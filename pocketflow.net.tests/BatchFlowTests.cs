using PocketFlow;

namespace PocketFlow.Tests;

public class BatchFlowTests
{
    [Fact]
    public async Task BatchFlow_basic_batch_processing()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_data"] = new Dictionary<string, int>
            {
                ["a"] = 1,
                ["b"] = 2,
                ["c"] = 3
            }
        };

        var processNode = new DataProcessNode();
        var batchFlow = new TestBatchFlow(processNode);
        
        await batchFlow.Run(shared);
        
        var results = (Dictionary<string, int>)shared["results"]!;
        Assert.Equal(2, results["a"]);
        Assert.Equal(4, results["b"]);
        Assert.Equal(6, results["c"]);
    }

    [Fact]
    public async Task BatchFlow_empty_input()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_data"] = new Dictionary<string, int>()
        };

        var processNode = new DataProcessNode();
        var batchFlow = new TestBatchFlow(processNode);
        
        await batchFlow.Run(shared);
        
        Assert.False(shared.ContainsKey("results"));
    }

    [Fact]
    public async Task BatchFlow_single_item()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_data"] = new Dictionary<string, int>
            {
                ["single"] = 5
            }
        };

        var processNode = new DataProcessNode();
        var batchFlow = new TestBatchFlow(processNode);
        
        await batchFlow.Run(shared);
        
        var results = (Dictionary<string, int>)shared["results"]!;
        Assert.Equal(10, results["single"]);
    }

    [Fact]
    public async Task BatchFlow_custom_params()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_data"] = new Dictionary<string, int>
            {
                ["a"] = 1,
                ["b"] = 2,
                ["c"] = 3
            }
        };

        var processNode = new CustomParamNode();
        var batchFlow = new CustomParamBatchFlow(processNode);
        
        await batchFlow.Run(shared);
        
        var results = (Dictionary<string, int>)shared["results"]!;
        Assert.Equal(1, results["a"]);
        Assert.Equal(4, results["b"]);
        Assert.Equal(9, results["c"]);
    }

    private class DataProcessNode : Node<Dictionary<string, object>, int, int>
    {
        public override Task<int> Prep(Dictionary<string, object> shared)
        {
            var key = Params["key"]?.ToString()!;
            var data = ((Dictionary<string, int>)shared["input_data"])[key];
            return Task.FromResult(data);
        }

        public override Task<int> Exec(int data)
            => Task.FromResult(data * 2);

        public override Task<string?> Post(Dictionary<string, object> shared, int p, int e)
        {
            var key = Params["key"]?.ToString()!;
            if (!shared.ContainsKey("results"))
                shared["results"] = new Dictionary<string, int>();
            ((Dictionary<string, int>)shared["results"])[key] = e;
            return Task.FromResult<string?>("default");
        }
    }

    private class TestBatchFlow : BatchFlow<Dictionary<string, object>>
    {
        public TestBatchFlow(BaseNode? start)
            : base(start, null, false)
        {
        }

        public override Task<IEnumerable<IDictionary<string, object>>> Prep(Dictionary<string, object> shared)
        {
            var keys = ((Dictionary<string, int>)shared["input_data"]).Keys;
            var items = keys.Select(k => new Dictionary<string, object> { ["key"] = k });
            return Task.FromResult<IEnumerable<IDictionary<string, object>>>(items.ToList());
        }
    }

    private class CustomParamNode : Node<Dictionary<string, object>, int, int>
    {
        public override Task<int> Prep(Dictionary<string, object> shared)
        {
            var key = Params["key"]?.ToString()!;
            var data = ((Dictionary<string, int>)shared["input_data"])[key];
            return Task.FromResult(data);
        }

        public override Task<int> Exec(int data)
        {
            var multiplier = (int)(Params["multiplier"] ?? 1);
            return Task.FromResult(data * multiplier);
        }

        public override Task<string?> Post(Dictionary<string, object> shared, int p, int e)
        {
            var key = Params["key"]?.ToString()!;
            if (!shared.ContainsKey("results"))
                shared["results"] = new Dictionary<string, int>();
            ((Dictionary<string, int>)shared["results"])[key] = e;
            return Task.FromResult<string?>("default");
        }
    }

    private class CustomParamBatchFlow : BatchFlow<Dictionary<string, object>>
    {
        public CustomParamBatchFlow(BaseNode? start)
            : base(start, null, false)
        {
        }

        public override Task<IEnumerable<IDictionary<string, object>>> Prep(Dictionary<string, object> shared)
        {
            var keys = ((Dictionary<string, int>)shared["input_data"]).Keys.ToList();
            var items = keys.Select((k, i) => new Dictionary<string, object> 
            { 
                ["key"] = k, 
                ["multiplier"] = i + 1 
            });
            return Task.FromResult<IEnumerable<IDictionary<string, object>>>(items.ToList());
        }
    }
}
