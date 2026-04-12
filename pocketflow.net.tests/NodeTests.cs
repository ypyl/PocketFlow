using PocketFlow;

namespace PocketFlow.Tests;

public class NodeTests
{
    [Fact]
    public async Task Node_with_default_post_returns_null()
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
    public async Task Node_can_store_results_in_shared()
    {
        var shared = new Dictionary<string, object>();
        var node = new ResultStoringNode();
        
        await node.Run(shared);
        
        Assert.Equal("test_data", shared["data"]);
        Assert.Equal("processed", shared["result"]);
    }

    [Fact]
    public async Task Node_with_retries_retries_until_success()
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

    [Fact]
    public async Task Node_fallback_not_called_on_success()
    {
        var shared = new Dictionary<string, object>();
        var node = new FallbackNode(shouldFail: false);
        
        var actionResult = await node.Run(shared);
        
        Assert.Equal("default", actionResult);
        Assert.Equal(1, node.AttemptCount);
        Assert.Single((List<object>)shared["results"]!);
        Assert.Equal("success", ((Dictionary<string, object>)((List<object>)shared["results"]!)[0])["result"]);
    }

    [Fact]
    public async Task Node_fallback_after_retries_exhausted()
    {
        var shared = new Dictionary<string, object>();
        var node = new FallbackNode(shouldFail: true, maxRetries: 2);
        
        var actionResult = await node.Run(shared);
        
        Assert.Equal("default", actionResult);
        Assert.Equal(2, node.AttemptCount);
        Assert.Single((List<object>)shared["results"]!);
        Assert.Equal("fallback", ((Dictionary<string, object>)((List<object>)shared["results"]!)[0])["result"]);
    }

    [Fact]
    public async Task Node_fallback_in_flow()
    {
        var shared = new Dictionary<string, object>();
        var fallbackNode = new FallbackNode(shouldFail: true);
        var resultNode = new ResultCollectNode();
        
        fallbackNode.On("default").To(resultNode);
        
        var pipeline = new Flow<Dictionary<string, object>>(fallbackNode);
        await pipeline.Run(shared);
        
        Assert.Single((List<object>)shared["results"]!);
        Assert.Equal("fallback", ((Dictionary<string, object>)((List<object>)shared["results"]!)[0])["result"]);
        Assert.NotNull(shared["final_result"]);
    }

    [Fact]
    public async Task Node_no_fallback_rethrows()
    {
        var shared = new Dictionary<string, object>();
        var node = new NoFallbackNode();
        
        await Assert.ThrowsAsync<Exception>(() => node.Run(shared));
    }

    [Fact]
    public async Task Node_retries_before_fallback()
    {
        var shared = new Dictionary<string, object>();
        var node = new FallbackNode(shouldFail: true, maxRetries: 3);
        
        var actionResult = await node.Run(shared);
        
        Assert.Equal("default", actionResult);
        Assert.Equal(3, node.AttemptCount);
        Assert.Equal("fallback", ((Dictionary<string, object>)((List<object>)shared["results"]!)[0])["result"]);
    }

    private class FallbackNode : Node<Dictionary<string, object>, object?, string>
    {
        public int AttemptCount { get; private set; }
        public bool ShouldFail { get; init; }

        public FallbackNode(bool shouldFail = true, int maxRetries = 1)
            : base(maxRetries, wait: 0)
        {
            ShouldFail = shouldFail;
        }

        public override Task<object?> Prep(Dictionary<string, object> shared)
        {
            if (!shared.ContainsKey("results"))
                shared["results"] = new List<object>();
            return Task.FromResult<object?>(null);
        }

        public override Task<string> Exec(object? prepRes)
        {
            AttemptCount++;
            if (ShouldFail)
                throw new Exception("Intentional failure");
            return Task.FromResult("success");
        }

        public override Task<string> ExecFallback(object? prepRes, Exception exc)
            => Task.FromResult("fallback");

        public override Task<string?> Post(Dictionary<string, object> shared, object? p, string e)
        {
            ((List<object>)shared["results"]!).Add(new Dictionary<string, object>
            {
                ["attempts"] = AttemptCount,
                ["result"] = e
            });
            return Task.FromResult<string?>("default");
        }
    }

    private class NoFallbackNode : Node<Dictionary<string, object>, object?, object?>
    {
        public override Task<object?> Prep(Dictionary<string, object> shared)
        {
            if (!shared.ContainsKey("results"))
                shared["results"] = new List<object>();
            return Task.FromResult<object?>(null);
        }

        public override Task<object?> Exec(object? prepRes)
            => throw new Exception("Test error");

        public override Task<string?> Post(Dictionary<string, object> shared, object? p, object? e)
        {
            ((List<object>)shared["results"]!).Add(new Dictionary<string, object>
            {
                ["result"] = e!
            });
            return Task.FromResult<string?>(null);
        }
    }

    private class ResultCollectNode : Node<Dictionary<string, object>, object?, object?>
    {
        public override Task<object?> Prep(Dictionary<string, object> shared)
            => Task.FromResult(shared.GetValueOrDefault("results"));

        public override Task<object?> Exec(object? prepRes)
            => Task.FromResult(prepRes);

        public override Task<string?> Post(Dictionary<string, object> shared, object? p, object? e)
        {
            shared["final_result"] = e;
            return Task.FromResult<string?>(null);
        }
    }
}