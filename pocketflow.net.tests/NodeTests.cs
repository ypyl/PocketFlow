using PocketFlow;

namespace PocketFlow.Tests;

public class NodeTests
{
    [Fact]
    public async Task node_with_default_post_returns_null()
    {
        var shared = new Dictionary<string, object>();
        var node = new TestNode();
        
        var result = await node.Run(shared);
        
        Assert.Null(result);
        Assert.True(node.PrepCalled);
        Assert.True(node.ExecCalled);
        Assert.True(node.PostCalled);
    }

    [Fact]
    public async Task node_can_store_results_in_shared()
    {
        var shared = new Dictionary<string, object>();
        var node = new ResultStoringNode();
        
        await node.Run(shared);
        
        Assert.Equal("test_data", shared["data"]);
        Assert.Equal("processed", shared["result"]);
    }

    [Fact]
    public async Task node_with_retries_retries_until_success()
    {
        var shared = new Dictionary<string, object>();
        var node = new RetryUntilSuccessNode(maxRetries: 3);
        
        await node.Run(shared);
        
        Assert.Equal(3, node.ExecCallCount);
    }

    private class TestNode : Node<Dictionary<string, object>, object?, object?>
    {
        public bool PrepCalled, ExecCalled, PostCalled;

        public override Task<object?> Prep(Dictionary<string, object> shared)
        {
            PrepCalled = true;
            return base.Prep(shared);
        }

        public override Task<object?> Exec(object? prepRes)
        {
            ExecCalled = true;
            return Task.FromResult<object?>("exec_result");
        }

        public override Task<string?> Post(Dictionary<string, object> shared, object? p, object? e)
        {
            PostCalled = true;
            return base.Post(shared, p, e);
        }
    }

    private class ResultStoringNode : Node<Dictionary<string, object>, string?, string?>
    {
        public override Task<string?> Prep(Dictionary<string, object> shared)
            => Task.FromResult<string?>("test_data");

        public override Task<string?> Exec(string? prepRes)
            => Task.FromResult<string?>("processed");

        public override Task<string?> Post(Dictionary<string, object> shared, string? p, string? e)
        {
            shared["data"] = p!;
            shared["result"] = e!;
            return Task.FromResult<string?>(null);
        }
    }

    private class RetryUntilSuccessNode : Node<Dictionary<string, object>, object?, object?>
    {
        public int ExecCallCount { get; private set; }

        public RetryUntilSuccessNode(int maxRetries = 1)
            : base(maxRetries, wait: 0)
        {
        }

        public override Task<object?> Exec(object? prepRes)
        {
            ExecCallCount++;
            if (ExecCallCount < MaxRetries)
                throw new Exception("Not yet");
            return Task.FromResult<object?>("success");
        }
    }
}