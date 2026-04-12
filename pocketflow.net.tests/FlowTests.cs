using PocketFlow;

namespace PocketFlow.Tests;

public class FlowTests
{
    [Fact]
    public async Task Flow_prep_can_be_overridden()
    {
        var shared = new Dictionary<string, object>();
        var node = new NoOpNode();
        var flow = new TestFlow(node);

        await flow.Run(shared);

        Assert.True(flow.PrepCalled);
    }
    
    [Fact]
    public void Flow_transition_setup_works()
    {
        var nodeA = new NoOpNode();
        var nodeB = new NoOpNode();
        
        nodeA.On("default").To(nodeB);
        
        Assert.Single(nodeA.Successors);
        Assert.True(nodeA.Successors.ContainsKey("default"));
        Assert.Same(nodeB, nodeA.Successors["default"]);
    }

    [Fact]
    public async Task Flow_can_be_initialized_with_start_node()
    {
        var shared = new Dictionary<string, object>();
        var n1 = new NumberNode(5);
        
        var pipeline = new Flow<Dictionary<string, object>>(n1);
        await pipeline.Run(shared);
        
        Assert.Equal(5, shared["current"]);
    }

    [Fact]
    public async Task Flow_sequence_with_explicit_transitions()
    {
        var shared = new Dictionary<string, object>();
        var n1 = new NumberNode(5);
        var n2 = new AddNode(3);
        var n3 = new MultiplyNode(2);
        
        n1.Next(n2);
        n2.Next(n3);
        
        var pipeline = new Flow<Dictionary<string, object>>(n1);
        await pipeline.Run(shared);
        
        Assert.Equal(16, shared["current"]);
    }

    [Fact]
    public async Task Flow_sequence_with_default_transitions()
    {
        var shared = new Dictionary<string, object>();
        var n1 = new NumberNode(5);
        var n2 = new AddNode(3);
        var n3 = new MultiplyNode(2);
        
        n1.On("default").To(n2);
        n2.On("default").To(n3);
        
        var pipeline = new Flow<Dictionary<string, object>>(n1);
        await pipeline.Run(shared);
        
        Assert.Equal(16, shared["current"]);
    }

    [Fact]
    public async Task Flow_branching_positive()
    {
        var shared = new Dictionary<string, object>();
        var startNode = new NumberNode(5);
        var checkNode = new CheckPositiveNode();
        var addIfPositive = new AddNode(10);
        var addIfNegative = new AddNode(-20);
        
        startNode.On("default").To(checkNode);
        checkNode.On("positive").To(addIfPositive);
        checkNode.On("negative").To(addIfNegative);
        
        var pipeline = new Flow<Dictionary<string, object>>(startNode);
        await pipeline.Run(shared);
        
        Assert.Equal(15, shared["current"]);
    }

    [Fact]
    public async Task Flow_branching_negative()
    {
        var shared = new Dictionary<string, object>();
        var startNode = new NumberNode(-5);
        var checkNode = new CheckPositiveNode();
        var addIfPositive = new AddNode(10);
        var addIfNegative = new AddNode(-20);
        
        startNode.On("default").To(checkNode);
        checkNode.On("positive").To(addIfPositive);
        checkNode.On("negative").To(addIfNegative);
        
        var pipeline = new Flow<Dictionary<string, object>>(startNode);
        await pipeline.Run(shared);
        
        Assert.Equal(-25, shared["current"]);
    }

    [Fact]
    public async Task Flow_cycle_until_condition_ends_with_signal()
    {
        var shared = new Dictionary<string, object>();
        var n1 = new NumberNode(10);
        var check = new CheckPositiveNode();
        var subtract3 = new AddNode(-3);
        var endNode = new EndSignalNode("cycle_done");
        
        n1.On("default").To(check);
        check.On("positive").To(subtract3);
        check.On("negative").To(endNode);
        subtract3.On("default").To(check);
        
        var pipeline = new Flow<Dictionary<string, object>>(n1);
        var lastAction = await pipeline.Run(shared);
        
        Assert.Equal(-2, shared["current"]);
        Assert.Equal("cycle_done", lastAction);
    }

    private class NoOpNode : Node<Dictionary<string, object>, object?, object?>
    {
        public bool Ran { get; private set; }

        public override Task<object?> Exec(object? prepRes)
        {
            Ran = true;
            return Task.FromResult<object?>(null);
        }

        public override Task<string?> Post(Dictionary<string, object> shared, object? p, object? e)
            => Task.FromResult<string?>(null);
    }

    private class TestFlow : Flow<Dictionary<string, object>>
    {
        public bool PrepCalled;

        public TestFlow(BaseNode? start = null) : base(start) { }

        public override Task Prep(Dictionary<string, object> shared)
        {
            PrepCalled = true;
            return base.Prep(shared);
        }
    }
}

public class NumberNode : Node<Dictionary<string, object>, object?, object?>
{
    public int Number { get; }
    
    public NumberNode(int number)
    {
        Number = number;
    }

    public override Task<object?> Prep(Dictionary<string, object> shared)
    {
        shared["current"] = Number;
        return Task.FromResult<object?>(null);
    }
}

public class AddNode : Node<Dictionary<string, object>, object?, object?>
{
    public int Number { get; }
    
    public AddNode(int number)
    {
        Number = number;
    }

    public override Task<object?> Prep(Dictionary<string, object> shared)
    {
        shared["current"] = (int)shared["current"] + Number;
        return Task.FromResult<object?>(null);
    }
}

public class MultiplyNode : Node<Dictionary<string, object>, object?, object?>
{
    public int Number { get; }
    
    public MultiplyNode(int number)
    {
        Number = number;
    }

    public override Task<object?> Prep(Dictionary<string, object> shared)
    {
        shared["current"] = (int)shared["current"] * Number;
        return Task.FromResult<object?>(null);
    }
}

public class CheckPositiveNode : Node<Dictionary<string, object>, object?, object?>
{
    public override Task<string?> Post(Dictionary<string, object> shared, object? p, object? e)
    {
        if ((int)shared["current"] >= 0)
            return Task.FromResult<string?>("positive");
        return Task.FromResult<string?>("negative");
    }
}

public class EndSignalNode : Node<Dictionary<string, object>, object?, object?>
{
    public string Signal { get; }
    
    public EndSignalNode(string signal)
    {
        Signal = signal;
    }

    public override Task<string?> Post(Dictionary<string, object> shared, object? p, object? e)
        => Task.FromResult<string?>(Signal);
}