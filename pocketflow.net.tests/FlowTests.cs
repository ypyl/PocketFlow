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