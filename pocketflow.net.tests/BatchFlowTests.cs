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

    [Fact]
    public async Task BatchFlow_error_handling()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_data"] = new Dictionary<string, int>
            {
                ["normal_key"] = 1,
                ["error_key"] = 2,
                ["another_key"] = 3
            }
        };

        var errorNode = new ErrorProcessNode();
        var batchFlow = new ErrorTestBatchFlow(errorNode);
        
        await Assert.ThrowsAsync<Exception>(() => batchFlow.Run(shared));
    }

    [Fact]
    public async Task BatchFlow_nested_flow()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_data"] = new Dictionary<string, int>
            {
                ["x"] = 1,
                ["y"] = 2
            }
        };

        var innerNode = new InnerProcessNode();
        var outerNode = new OuterProcessNode();
        innerNode.On("default").To(outerNode);

        var batchFlow = new NestedFlowTestBatchFlow(innerNode);
        await batchFlow.Run(shared);

        var results = (Dictionary<string, int>)shared["results"]!;
        Assert.Equal(4, results["x"]);
        Assert.Equal(6, results["y"]);
    }

    [Fact]
    public async Task BatchFlow_nested_batch_flow()
    {
        var shared = new Dictionary<string, object>
        {
            ["groups"] = new Dictionary<string, List<int>>
            {
                ["A"] = new List<int> { 1, 2 },
                ["B"] = new List<int> { 3, 4 }
            }
        };

        var itemNode = new NestedItemNode();
        var innerBatchFlow = new InnerBatchFlow(itemNode);
        var outerBatchFlow = new OuterBatchFlow(innerBatchFlow);

        await outerBatchFlow.Run(shared);

        var results = (Dictionary<string, List<int>>)shared["results"]!;
        Assert.Equal(2, results["A"].Count);
        Assert.Equal(2, results["A"][0]);
        Assert.Equal(4, results["A"][1]);
        Assert.Equal(6, results["B"][0]);
        Assert.Equal(8, results["B"][1]);
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

    private class ErrorProcessNode : Node<Dictionary<string, object>, int, int>
    {
        public override Task<int> Prep(Dictionary<string, object> shared)
        {
            var key = Params["key"]?.ToString()!;
            return Task.FromResult(key == "error_key" ? 1 : 0);
        }

        public override Task<int> Exec(int p)
        {
            if (p == 1) throw new Exception("Error processing key");
            return Task.FromResult(p);
        }

        public override Task<string?> Post(Dictionary<string, object> shared, int p, int e)
            => Task.FromResult<string?>("default");
    }

    private class ErrorTestBatchFlow : BatchFlow<Dictionary<string, object>>
    {
        public ErrorTestBatchFlow(BaseNode? start)
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

    private class InnerProcessNode : Node<Dictionary<string, object>, int, int>
    {
        public override Task<int> Prep(Dictionary<string, object> shared)
        {
            var key = Params["key"]?.ToString()!;
            var data = ((Dictionary<string, int>)shared["input_data"])[key];
            return Task.FromResult(data);
        }

        public override Task<int> Exec(int data)
            => Task.FromResult(data + 1);

        public override Task<string?> Post(Dictionary<string, object> shared, int p, int e)
        {
            var key = Params["key"]?.ToString()!;
            if (!shared.ContainsKey("intermediate_results"))
                shared["intermediate_results"] = new Dictionary<string, int>();
            ((Dictionary<string, int>)shared["intermediate_results"])[key] = e;
            return Task.FromResult<string?>("default");
        }
    }

    private class OuterProcessNode : Node<Dictionary<string, object>, int, int>
    {
        public override Task<int> Prep(Dictionary<string, object> shared)
        {
            var key = Params["key"]?.ToString()!;
            var data = ((Dictionary<string, int>)shared["intermediate_results"])[key];
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

    private class NestedFlowTestBatchFlow : BatchFlow<Dictionary<string, object>>
    {
        public NestedFlowTestBatchFlow(BaseNode? start)
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

    private class NestedItemNode : Node<Dictionary<string, object>, int, int>
    {
        public override Task<int> Prep(Dictionary<string, object> shared)
        {
            var group = Params["group"]?.ToString()!;
            var itemIndex = (int)Params["item"]!;
            var data = ((Dictionary<string, List<int>>)shared["groups"])[group][itemIndex];
            return Task.FromResult(data);
        }

        public override Task<int> Exec(int data)
            => Task.FromResult(data * 2);

        public override Task<string?> Post(Dictionary<string, object> shared, int p, int e)
        {
            var group = Params["group"]?.ToString()!;
            if (!shared.ContainsKey("results"))
                shared["results"] = new Dictionary<string, List<int>>();
            if (!((Dictionary<string, List<int>>)shared["results"]).ContainsKey(group))
                ((Dictionary<string, List<int>>)shared["results"])[group] = new List<int>();
            ((Dictionary<string, List<int>>)shared["results"])[group].Add(e);
            return Task.FromResult<string?>("default");
        }
    }

    private class InnerBatchFlow : BatchFlow<Dictionary<string, object>>
    {
        public InnerBatchFlow(BaseNode? start)
            : base(start, null, false)
        {
        }

        public override Task<IEnumerable<IDictionary<string, object>>> Prep(Dictionary<string, object> shared)
        {
            var group = Params["group"]?.ToString()!;
            var items = ((Dictionary<string, List<int>>)shared["groups"])[group];
            return Task.FromResult<IEnumerable<IDictionary<string, object>>>(
                items.Select((_, i) => new Dictionary<string, object> { ["item"] = i, ["group"] = group }));
        }
    }

    private class OuterBatchFlow : BatchFlow<Dictionary<string, object>>
    {
        public OuterBatchFlow(BaseNode? start)
            : base(start, null, false)
        {
        }

        public override Task<IEnumerable<IDictionary<string, object>>> Prep(Dictionary<string, object> shared)
        {
            var groups = ((Dictionary<string, List<int>>)shared["groups"]).Keys;
            return Task.FromResult<IEnumerable<IDictionary<string, object>>>(
                groups.Select(g => new Dictionary<string, object> { ["group"] = g }));
        }
    }
}
