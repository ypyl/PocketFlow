
 public interface INode<TShared, TPrepReturn, TExecReturn>
 {
     Task<TPrepReturn?> Prep(TShared shared);
     Task<TExecReturn?> Exec(TPrepReturn? prepRes);
     Task<string?> Post(TShared shared, TPrepReturn? prepRes, TExecReturn? execRes);
 }

 public class BaseNode
 {
     protected ImmutableDictionary<string, object> Params = ImmutableDictionary<string, object>.Empty;
     public ImmutableDictionary<string, BaseNode> Successors = ImmutableDictionary<string, BaseNode>.Empty;

     public void SetParams(Dictionary<string, object> p) => Params = p.ToImmutableDictionary();

     public BaseNode ShallowClone() => (BaseNode)MemberwiseClone();

     public BaseNode Next(BaseNode node, string action = "default")
     {
         if (Successors.ContainsKey(action))
             Console.Error.WriteLine($"Overwriting successor for action '{action}'");
         Successors = Successors.SetItem(action, node);
         return node;
     }

     public ConditionalTransition this[string action] => new(this, action);
 }

 public class ConditionalTransition(BaseNode src, string action)
 {
     public readonly BaseNode Src = src;
     public readonly string Action = action;

     public BaseNode To(BaseNode target) => Src.Next(target, Action);
 }

 public interface INodeInternal<TShared>
 {
     Task<string?> RunInternal(TShared shared);
 }


 public class Node<TShared, TPrepReturn, TExecReturn>(int maxRetries = 1, double wait = 0) : BaseNode, INode<TShared, TPrepReturn, TExecReturn>, INodeInternal<TShared>
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
         if (Successors.Count > 0)
             Console.Error.WriteLine("Node won't run successors. Use Flow.");
         return await RunInternal(shared);
     }

     public virtual async Task<string?> RunInternal(TShared shared)
     {
         var p = await Prep(shared);
         var e = await Execute(p);
         return await Post(shared, p, e);
     }
 }

 public class BatchNode<TShared, TPrepReturn, TExecReturn>(int maxRetries = 1, double wait = 0) : Node<TShared, IEnumerable<TPrepReturn?>, IEnumerable<TExecReturn?>>(maxRetries, wait)
 {
     public virtual Task<TExecReturn?> ExecSingle(TPrepReturn? prepRes) => Task.FromResult<TExecReturn?>(default);
     public virtual Task<TExecReturn?> ExecSingleFallback(TPrepReturn? prepRes, Exception exc)
     {
         ExceptionDispatchInfo.Capture(exc).Throw();
         return Task.FromResult<TExecReturn?>(default);
     }

     protected override async Task<IEnumerable<TExecReturn?>?> Execute(IEnumerable<TPrepReturn?>? items)
     {
         if (items is null) return [];
         var results = new List<TExecReturn?>();
         foreach (var i in items)
         {
             for (var retry = 0; retry < MaxRetries; retry++)
             {
                 try { results.Add(await ExecSingle(i)); break; }
                 catch (Exception e)
                 {
                     if (retry == MaxRetries - 1) results.Add(await ExecSingleFallback(i, e));
                     else if (Wait > 0) await Task.Delay((int)(Wait * 1000));
                 }
             }
         }
         return results;
     }
 }

 public class ParallelBatchNode<TShared, TPrepReturn, TExecReturn>(int maxRetries = 1, double wait = 0) : BatchNode<TShared, TPrepReturn, TExecReturn>(maxRetries, wait)
 {
     protected override async Task<IEnumerable<TExecReturn?>?> Execute(IEnumerable<TPrepReturn?>? items)
     {
         if (items is null) return [];
         return await Task.WhenAll(items.Select(i => ExecSingle(i)));
     }
 }

 public class Flow<TShared>(BaseNode? start = null) : Node<TShared, object?, string?>
 {
     protected BaseNode? StartNode = start;

     public BaseNode Start(BaseNode s) { StartNode = s; return s; }

     protected BaseNode? GetNextNode(BaseNode curr, string? action)
     {
         var key = action ?? "default";
         if (curr.Successors.TryGetValue(key, out var nxt)) return nxt;
         if (curr.Successors.Count > 0)
             Console.Error.WriteLine($"Flow ends: '{key}' not found in [{string.Join(", ", curr.Successors.Keys)}]");
         return null;
     }

     protected async Task<string?> Orchestrate(TShared shared, Dictionary<string, object>? p = null)
     {
         var curr = Clone(StartNode);
         var parms = p ?? new(Params);
         string? lastAction = null;
         while (curr != null)
         {
             curr.SetParams(parms);
             lastAction = curr is INodeInternal<TShared> an
                 ? await an.RunInternal(shared)
                 : null;
             curr = Clone(GetNextNode(curr, lastAction));
         }
         return lastAction;
     }

     public override async Task<string?> RunInternal(TShared shared)
     {
         var p = await Prep(shared);
         var o = await Orchestrate(shared);
         return await Post(shared, p, o);
     }

     public override Task<string?> Post(TShared shared, object? prepRes, string? execRes)
         => Task.FromResult(execRes);

     protected static BaseNode? Clone(BaseNode? node) => node?.ShallowClone();
 }

 public class BatchFlow<TShared>(BaseNode? start = null) : Flow<TShared>(start)
 {
     public override async Task<string?> RunInternal(TShared shared)
     {
         var pr = (await Prep(shared) as IEnumerable<Dictionary<string, object>>) ?? [];
         var results = new List<string?>();
         foreach (var bp in pr)
             results.Add(await Orchestrate(shared, new Dictionary<string, object>(Params).Concat(bp).ToDictionary(k => k.Key, k => k.Value)));
         return await Post(shared, pr, results.LastOrDefault());
     }
 }

 public class ParallelBatchFlow<TShared>(BaseNode? start = null) : Flow<TShared>(start)
 {
     public override async Task<string?> RunInternal(TShared shared)
     {
         var pr = (await Prep(shared) as IEnumerable<Dictionary<string, object>>) ?? [];
         await Task.WhenAll(pr.Select(bp =>
             Orchestrate(shared, new Dictionary<string, object>(Params).Concat(bp).ToDictionary(k => k.Key, k => k.Value))));
         return await Post(shared, pr, null);
     }
 }

 // Non-generic aliases for Dictionary<string, object> shared
 public class Node(int maxRetries = 1, double wait = 0) : Node<Dictionary<string, object>, object?, object?>(maxRetries, wait);
 public class BatchNode(int maxRetries = 1, double wait = 0) : BatchNode<Dictionary<string, object>, object?, object?>(maxRetries, wait);
 public class ParallelBatchNode(int maxRetries = 1, double wait = 0) : ParallelBatchNode<Dictionary<string, object>, object?, object?>(maxRetries, wait);
 public class Flow(BaseNode? start = null) : Flow<Dictionary<string, object>>(start);
 public class BatchFlow(BaseNode? start = null) : BatchFlow<Dictionary<string, object>>(start);
 public class ParallelBatchFlow(BaseNode? start = null) : ParallelBatchFlow<Dictionary<string, object>>(start);
