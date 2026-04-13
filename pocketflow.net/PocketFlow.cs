using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace PocketFlow;

public interface IOrchestrated<TShared>
{
    Task<string?> Run(TShared shared);
}

public interface INode<TShared, TPrepReturn, TExecReturn> : IOrchestrated<TShared>
{
    Task<TPrepReturn?> Prep(TShared shared);
    Task<TExecReturn?> Exec(TPrepReturn? prepRes);
    Task<string?> Post(TShared shared, TPrepReturn? prepRes, TExecReturn? execRes);
}

public interface IFlow<TShared> : IOrchestrated<TShared>
{
    Task Prep(TShared shared);
    Task<string?> Orchestrate(TShared shared);
    Task<string?> Post(TShared shared, string? nextAction);
}

public class BaseNode
{
    public ImmutableDictionary<string, BaseNode> Successors = ImmutableDictionary<string, BaseNode>.Empty;
    public IDictionary<string, object> Params { get; private set; } = new Dictionary<string, object>();

    public void SetParams(IDictionary<string, object>? parameters)
    {
        Params.Clear();
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                Params[kvp.Key] = kvp.Value;
            }
        }
    }

    public BaseNode ShallowClone()
    {
        var clone = (BaseNode)MemberwiseClone();
        clone.Params = Params;
        return clone;
    }

    public BaseNode Next(BaseNode node, string action = "default")
    {
        if (Successors.ContainsKey(action))
            Console.Error.WriteLine($"Overwriting successor for action '{action}'");
        Successors = Successors.SetItem(action, node);
        return node;
    }

    public TransitionBuilder On(string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action, "Action must be non-empty");
        return new TransitionBuilder(this, action);
    }
}

public class TransitionBuilder(BaseNode source, string action)
{
    public BaseNode To(BaseNode target) => source.Next(target, action);
}

public class Node<TShared, TPrepReturn, TExecReturn>(int maxRetries = 1, double wait = 0) : BaseNode, INode<TShared, TPrepReturn, TExecReturn>
{
    protected int MaxRetries = maxRetries;
    protected double Wait = wait;
    protected int CurRetry;

    public virtual Task<TPrepReturn?> Prep(TShared shared) => Task.FromResult<TPrepReturn?>(default);
    public virtual Task<TExecReturn?> Exec(TPrepReturn? prepRes) => Task.FromResult<TExecReturn?>(default);
    public virtual Task<TExecReturn?> ExecFallback(TPrepReturn? prepRes, Exception exc)
    {
        ExceptionDispatchInfo.Capture(exc).Throw();
        return Task.FromResult<TExecReturn?>(default);
    }
    public virtual Task<string?> Post(TShared shared, TPrepReturn? prepRes, TExecReturn? execRes) => Task.FromResult<string?>(null);

    protected virtual async Task<TExecReturn?> Execute(TPrepReturn? prepRes)
    {
        for (CurRetry = 0; CurRetry < MaxRetries; CurRetry++)
        {
            try { return await Exec(prepRes); }
            catch (Exception e)
            {
                if (CurRetry == MaxRetries - 1) return await ExecFallback(prepRes, e);
                if (Wait > 0) await Task.Delay((int)(Wait * 1000));
            }
        }
        return default;
    }

    public async Task<string?> Run(TShared shared)
    {
        var p = await Prep(shared);
        var e = await Execute(p);
        return await Post(shared, p, e);
    }
}

public class Flow<TShared>(BaseNode? start = null) : BaseNode, IFlow<TShared>
{
    protected BaseNode? StartNode = start;

    protected BaseNode? GetNextNode(BaseNode curr, string? action)
    {
        var key = action ?? "default";
        if (curr.Successors.TryGetValue(key, out var nxt)) return nxt;
        if (curr.Successors.Count > 0)
            Console.Error.WriteLine($"Flow ends: '{key}' not found in [{string.Join(", ", curr.Successors.Keys)}]");
        return null;
    }

    public virtual Task Prep(TShared shared) => Task.CompletedTask;

    protected virtual async Task<string?> DoOrchestrate(TShared shared)
    {
        var curr = Clone(StartNode);
        string? lastAction = null;
        while (curr != null)
        {
            lastAction = curr is IOrchestrated<TShared> an
                ? await an.Run(shared)
                : null;
            var nextNode = GetNextNode(curr, lastAction);
            if (nextNode != null)
            {
                nextNode.SetParams(curr.Params);
            }
            curr = Clone(nextNode);
        }
        return lastAction;
    }

    public virtual Task<string?> Orchestrate(TShared shared) => DoOrchestrate(shared);

    public virtual Task<string?> Post(TShared shared, string? nextAction)
        => Task.FromResult(nextAction);

    protected static BaseNode? Clone(BaseNode? node) => node?.ShallowClone();

    public async Task<string?> Run(TShared shared)
    {
        await Prep(shared);
        var o = await Orchestrate(shared);
        return await Post(shared, o);
    }
}

public class BatchNode<TShared, TItem, TExecReturn>(
    IDictionary<string, object>? defaultParams = null,
    int maxRetries = 1,
    double wait = 0,
    bool enableParallel = false
) : BaseNode, IOrchestrated<TShared>
{
    public IDictionary<string, object> DefaultParams { get; } = defaultParams ?? new Dictionary<string, object>();
    public bool EnableParallel { get; } = enableParallel;
    protected int MaxRetries = maxRetries;
    protected double Wait = wait;
    protected int CurRetry;

    public virtual Task<IEnumerable<TItem>?> Prep(TShared shared) 
        => Task.FromResult<IEnumerable<TItem>?>([]);

    public virtual Task<TExecReturn> ExecItem(TItem item) 
        => Task.FromResult<TExecReturn>(default!);

    public virtual Task<TExecReturn> ExecFallback(TItem item, Exception exc)
    {
        ExceptionDispatchInfo.Capture(exc).Throw();
        return Task.FromResult<TExecReturn>(default!);
    }

    public virtual Task<string?> Post(TShared shared, IEnumerable<TItem>? prepRes, IList<TExecReturn>? execRes) 
        => Task.FromResult<string?>(null);

    private async Task<TExecReturn> ExecItemWithRetry(TItem item)
    {
        for (CurRetry = 0; CurRetry < MaxRetries; CurRetry++)
        {
            try { return await ExecItem(item); }
            catch (Exception e)
            {
                if (CurRetry == MaxRetries - 1) return await ExecFallback(item, e);
                if (Wait > 0) await Task.Delay((int)(Wait * 1000));
            }
        }
        return default!;
    }

    public async Task<string?> Run(TShared shared)
    {
        var items = await Prep(shared);
        var results = await Execute(items);
        return await Post(shared, items, results);
    }

    private async Task<IList<TExecReturn>> Execute(IEnumerable<TItem>? items)
    {
        var results = new List<TExecReturn>();
        var itemList = items?.ToList() ?? [];
        
        if (EnableParallel)
        {
            var tasks = itemList.Select(ExecItemWithRetry);
            var taskResults = await Task.WhenAll(tasks);
            results.AddRange(taskResults);
        }
        else
        {
            foreach (var item in itemList)
            {
                results.Add(await ExecItemWithRetry(item));
            }
        }
        
        return results;
    }
}

public class BatchFlow<TShared>(
    BaseNode? start = null,
    IDictionary<string, object>? defaultParams = null,
    bool enableParallel = false
) : Flow<TShared>(start) where TShared : IDictionary<string, object>
{
    public IDictionary<string, object> DefaultParams { get; } = defaultParams ?? new Dictionary<string, object>();
    public bool EnableParallel { get; } = enableParallel;
    public IDictionary<string, object>? CurrentItemParams { get; private set; }

    public override Task<IEnumerable<IDictionary<string, object>>> Prep(TShared shared) 
        => Task.FromResult<IEnumerable<IDictionary<string, object>>>([]);

    protected virtual async Task OrchestrateOnce(TShared shared, IDictionary<string, object> itemParams)
    {
        CurrentItemParams = MergeParams(DefaultParams, itemParams);
        if (StartNode != null)
        {
            StartNode.SetParams(CurrentItemParams);
        }
        await base.DoOrchestrate(shared);
    }

    protected override async Task<string?> DoOrchestrate(TShared shared)
    {
        var paramSets = (await Prep(shared))?.ToList() ?? [];
        
        if (EnableParallel)
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = -1 };
            await Parallel.ForEachAsync(paramSets, options, async (itemParams, ct) =>
            {
                await OrchestrateOnce(shared, itemParams);
            });
        }
        else
        {
            foreach (var itemParams in paramSets)
            {
                if (StartNode != null)
                {
                    StartNode.SetParams(itemParams);
                }
                await base.DoOrchestrate(shared);
            }
        }
        
        return null;
    }

    private static IDictionary<string, object> MergeParams(
        IDictionary<string, object> defaultParams, 
        IDictionary<string, object> itemParams)
    {
        var merged = new Dictionary<string, object>(defaultParams);
        foreach (var kvp in itemParams)
        {
            merged[kvp.Key] = kvp.Value;
        }
        return merged;
    }
}
