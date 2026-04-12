using System.Collections.Immutable;
using System.Runtime.ExceptionServices;

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

    public BaseNode ShallowClone() => (BaseNode)MemberwiseClone();

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
    private BaseNode? _lastNode;

    public Flow<TShared> Start(BaseNode node)
    {
        StartNode = node;
        _lastNode = node;
        return this;
    }

    public new Flow<TShared> Next(BaseNode node, string action = "default")
    {
        if (_lastNode == null)
            throw new InvalidOperationException("Start node not set. Call Start() first.");
        _lastNode.Next(node, action);
        _lastNode = node;
        return this;
    }

    protected BaseNode? GetNextNode(BaseNode curr, string? action)
    {
        var key = action ?? "default";
        if (curr.Successors.TryGetValue(key, out var nxt)) return nxt;
        if (curr.Successors.Count > 0)
            Console.Error.WriteLine($"Flow ends: '{key}' not found in [{string.Join(", ", curr.Successors.Keys)}]");
        return null;
    }

    public virtual Task Prep(TShared shared) => Task.CompletedTask;

    public virtual async Task<string?> Orchestrate(TShared shared)
    {
        var curr = Clone(StartNode);
        string? lastAction = null;
        while (curr != null)
        {
            lastAction = curr is IOrchestrated<TShared> an
                ? await an.Run(shared)
                : null;
            curr = Clone(GetNextNode(curr, lastAction));
        }
        return lastAction;
    }

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