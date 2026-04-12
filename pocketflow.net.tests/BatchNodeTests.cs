using PocketFlow;

namespace PocketFlow.Tests;

public class BatchNodeTests
{
    [Fact]
    public async Task BatchNode_array_chunking()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_array"] = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 }
        };
        
        var chunkNode = new ArrayChunkNode(10);
        await chunkNode.Run(shared);
        
        var results = (List<int>)shared["chunk_results"]!;
        Assert.Equal(45, results[0]);
        Assert.Equal(145, results[1]);
        Assert.Equal(110, results[2]);
    }

    [Fact]
    public async Task BatchNode_map_reduce_sum()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_array"] = Enumerable.Range(0, 100).ToList()
        };
        
        var chunkNode = new ArrayChunkNode(10);
        var reduceNode = new SumReduceNode();
        
        chunkNode.On("done").To(reduceNode);
        
        var pipeline = new Flow<Dictionary<string, object>>(chunkNode);
        await pipeline.Run(shared);
        
        Assert.Equal(4950, shared["total"]);
    }

    [Fact]
    public async Task BatchNode_uneven_chunks()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_array"] = Enumerable.Range(0, 25).ToList()
        };
        
        var chunkNode = new ArrayChunkNode(10);
        var reduceNode = new SumReduceNode();
        
        chunkNode.On("done").To(reduceNode);
        
        var pipeline = new Flow<Dictionary<string, object>>(chunkNode);
        await pipeline.Run(shared);
        
        Assert.Equal(300, shared["total"]);
    }

    [Fact]
    public async Task BatchNode_custom_chunk_size()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_array"] = Enumerable.Range(0, 100).ToList()
        };
        
        var chunkNode = new ArrayChunkNode(15);
        var reduceNode = new SumReduceNode();
        
        chunkNode.On("done").To(reduceNode);
        
        var pipeline = new Flow<Dictionary<string, object>>(chunkNode);
        await pipeline.Run(shared);
        
        Assert.Equal(4950, shared["total"]);
    }

    [Fact]
    public async Task BatchNode_single_element_chunks()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_array"] = Enumerable.Range(0, 5).ToList()
        };
        
        var chunkNode = new ArrayChunkNode(1);
        var reduceNode = new SumReduceNode();
        
        chunkNode.On("done").To(reduceNode);
        
        var pipeline = new Flow<Dictionary<string, object>>(chunkNode);
        await pipeline.Run(shared);
        
        Assert.Equal(10, shared["total"]);
    }

    [Fact]
    public async Task BatchNode_empty_array()
    {
        var shared = new Dictionary<string, object>
        {
            ["input_array"] = new List<int>()
        };
        
        var chunkNode = new ArrayChunkNode(10);
        var reduceNode = new SumReduceNode();
        
        chunkNode.On("done").To(reduceNode);
        
        var pipeline = new Flow<Dictionary<string, object>>(chunkNode);
        await pipeline.Run(shared);
        
        Assert.Equal(0, shared["total"]);
    }

    private class ArrayChunkNode : BatchNode<Dictionary<string, object>, int, int>
    {
        private readonly int _chunkSize;

        public ArrayChunkNode(int chunkSize = 10)
            : base(null, 1, 0, false)
        {
            _chunkSize = chunkSize;
        }

        public override Task<IEnumerable<int>?> Prep(Dictionary<string, object> shared)
        {
            var array = (List<int>)shared["input_array"];
            var chunks = new List<int>();
            for (var i = 0; i < array.Count; i += _chunkSize)
            {
                var end = Math.Min(i + _chunkSize, array.Count);
                var chunkSum = 0;
                for (var j = i; j < end; j++)
                    chunkSum += array[j];
                chunks.Add(chunkSum);
            }
            return Task.FromResult<IEnumerable<int>?>(chunks);
        }

        public override Task<int> ExecItem(int chunkSum)
        {
            return Task.FromResult(chunkSum);
        }

        public override Task<string?> Post(Dictionary<string, object> shared, IEnumerable<int>? p, IList<int>? results)
        {
            shared["chunk_results"] = results!;
            return Task.FromResult<string?>("done");
        }
    }

    private class SumReduceNode : Node<Dictionary<string, object>, object?, int>
    {
        public override Task<object?> Prep(Dictionary<string, object> shared)
        {
            var chunkResults = (List<int>)shared["chunk_results"];
            return Task.FromResult<object?>(chunkResults);
        }

        public override Task<int> Exec(object? prepRes)
            => Task.FromResult(0);

        public override Task<string?> Post(Dictionary<string, object> shared, object? p, int e)
        {
            var chunkResults = (List<int>)p!;
            shared["total"] = chunkResults.Sum();
            return Task.FromResult<string?>(null);
        }
    }
}
