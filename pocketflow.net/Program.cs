using PocketFlow;

var shared = new Dictionary<string, object>();

var nodeA = new MyNodeA();
var nodeB = new MyNodeB();
nodeA.On("next").To(nodeB);

var flow = new Flow<Dictionary<string, object>>(nodeA);
await flow.Run(shared);

class MyNodeA : Node<Dictionary<string, object>, object?, object?>
{
    public override Task<object?> Exec(object? _) { Console.WriteLine("Node A"); return Task.FromResult<object?>(null); }
    public override Task<string?> Post(Dictionary<string, object> shared, object? p, object? e) => Task.FromResult<string?>("default");
}

class MyNodeB : Node<Dictionary<string, object>, object?, object?>
{
    public override Task<object?> Exec(object? _) { Console.WriteLine("Node B"); return Task.FromResult<object?>(null); }
}
