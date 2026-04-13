#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class ChunkDocumentsNode : BatchNode<TShared, string, List<string>>
{
    public override Task<IEnumerable<string>?> Prep(TShared shared)
    {
        var texts = shared.TryGetValue("texts", out var t) ? t as List<string> ?? new List<string>() : new List<string>();
        return Task.FromResult<IEnumerable<string>?>(texts);
    }

    public override Task<List<string>> ExecItem(string text)
    {
        var chunks = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunkSize = 50;
        
        for (int i = 0; i < words.Length; i += chunkSize)
        {
            var chunk = string.Join(" ", words.Skip(i).Take(chunkSize));
            chunks.Add(chunk);
        }
        
        return Task.FromResult(chunks);
    }

    public override Task<string?> Post(TShared shared, IEnumerable<string>? prepRes, IList<List<string>>? execRes)
    {
        var allChunks = new List<string>();
        if (execRes != null)
        {
            foreach (var chunks in execRes)
            {
                allChunks.AddRange(chunks);
            }
        }
        
        shared["texts"] = allChunks;
        Console.WriteLine($"Created {allChunks.Count} chunks from {(prepRes?.Count() ?? 0)} documents");
        return Task.FromResult<string?>("default");
    }
}

class EmbedDocumentsNode : BatchNode<TShared, string, double[]>
{
    public override Task<IEnumerable<string>?> Prep(TShared shared)
    {
        var texts = shared.TryGetValue("texts", out var t) ? t as List<string> ?? new List<string>() : new List<string>();
        return Task.FromResult<IEnumerable<string>?>(texts);
    }

    public override Task<double[]> ExecItem(string text)
    {
        var embedding = MockGetEmbedding(text);
        return Task.FromResult(embedding);
    }

    public override Task<string?> Post(TShared shared, IEnumerable<string>? prepRes, IList<double[]>? execRes)
    {
        var embeddings = execRes?.ToList() ?? new List<double[]>();
        shared["embeddings"] = embeddings;
        Console.WriteLine($"Created {embeddings.Count} document embeddings");
        return Task.FromResult<string?>("default");
    }

    private double[] MockGetEmbedding(string text)
    {
        var random = new Random(text.GetHashCode());
        return Enumerable.Range(0, 128).Select(_ => random.NextDouble()).ToArray();
    }
}

class CreateIndexNode : Node<TShared, List<double[]>, object>
{
    public override Task<List<double[]>?> Prep(TShared shared)
    {
        if (shared.TryGetValue("embeddings", out var emb) && emb is List<double[]> embeddings)
            return Task.FromResult<List<double[]>?>(embeddings);
        return Task.FromResult<List<double[]>?>(null);
    }

    public override Task<object?> Exec(List<double[]>? embeddings)
    {
        if (embeddings == null || embeddings.Count == 0)
            return Task.FromResult<object?>(null);
        
        Console.WriteLine("Creating search index...");
        
        var index = new MockIndex(embeddings);
        return Task.FromResult<object?>(index);
    }

    public override Task<string?> Post(TShared shared, List<double[]>? prepRes, object? execRes)
    {
        if (execRes is MockIndex index)
        {
            shared["index"] = index;
            Console.WriteLine($"Index created with {index.Count} vectors");
        }
        return Task.FromResult<string?>("default");
    }
}

class MockIndex
{
    public List<double[]> Embeddings { get; }
    public int Count => Embeddings.Count;
    
    public MockIndex(List<double[]> embeddings)
    {
        Embeddings = embeddings;
    }
    
    public (int Index, double Distance) Search(double[] query, int k = 1)
    {
        double minDist = double.MaxValue;
        int minIdx = 0;
        
        for (int i = 0; i < Embeddings.Count; i++)
        {
            var dist = CosineDistance(query, Embeddings[i]);
            if (dist < minDist)
            {
                minDist = dist;
                minIdx = i;
            }
        }
        
        return (minIdx, minDist);
    }
    
    private double CosineDistance(double[] a, double[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return 1 - (dot / (Math.Sqrt(magA) * Math.Sqrt(magB)));
    }
}

class EmbedQueryNode : Node<TShared, string?, double[]>
{
    public override Task<string?> Prep(TShared shared)
    {
        var query = shared.TryGetValue("query", out var q) ? q as string : null;
        return Task.FromResult(query);
    }

    public override Task<double[]?> Exec(string? query)
    {
        if (query == null)
            return Task.FromResult<double[]?>(null);
        
        Console.WriteLine($"Embedding query: {query}");
        var embedding = MockGetEmbedding(query);
        return Task.FromResult<double[]?>(embedding);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, double[]? execRes)
    {
        if (execRes != null)
            shared["query_embedding"] = execRes;
        return Task.FromResult<string?>("default");
    }

    private double[] MockGetEmbedding(string text)
    {
        var random = new Random(text.GetHashCode());
        return Enumerable.Range(0, 128).Select(_ => random.NextDouble()).ToArray();
    }
}

class RetrieveDocumentNode : Node<TShared, (double[] QueryEmbedding, object Index, List<string> Texts)?, Dictionary<string, object>>
{
    public override Task<(double[], object, List<string>)?> Prep(TShared shared)
    {
        var queryEmbedding = shared.TryGetValue("query_embedding", out var qe) ? qe as double[] : null;
        var index = shared.TryGetValue("index", out var i) ? i : null;
        var texts = shared.TryGetValue("texts", out var t) ? t as List<string> ?? new List<string>() : new List<string>();
        
        if (queryEmbedding == null || index == null)
            return Task.FromResult<(double[], object, List<string>)?>(null);
        
        return Task.FromResult<(double[], object, List<string>)?>((queryEmbedding, index, texts));
    }

    public override Task<Dictionary<string, object>?> Exec((double[], object, List<string>)? inputs)
    {
        if (inputs == null)
            return Task.FromResult<Dictionary<string, object>?>(null);
        
        var (queryEmbedding, indexObj, texts) = inputs.Value;
        Console.WriteLine("Searching for relevant documents...");
        
        if (indexObj is MockIndex index)
        {
            var (bestIdx, distance) = index.Search(queryEmbedding, 1);
            var mostRelevantText = texts[bestIdx];
            
            var result = new Dictionary<string, object>
            {
                ["text"] = mostRelevantText,
                ["index"] = bestIdx,
                ["distance"] = distance
            };
            
            return Task.FromResult<Dictionary<string, object>?>(result);
        }
        
        return Task.FromResult<Dictionary<string, object>?>(null);
    }

    public override Task<string?> Post(TShared shared, (double[], object, List<string>)? prepRes, Dictionary<string, object>? execRes)
    {
        if (execRes != null)
        {
            shared["retrieved_document"] = execRes;
            Console.WriteLine($"Retrieved document (index: {execRes["index"]}, distance: {execRes["distance"]:F4})");
            Console.WriteLine($"Most relevant text: \"{execRes["text"]}\"");
        }
        return Task.FromResult<string?>("default");
    }
}

class GenerateAnswerNode : Node<TShared, (string Query, Dictionary<string, object> RetrievedDoc)?, string>
{
    public override Task<(string, Dictionary<string, object>)?> Prep(TShared shared)
    {
        var query = shared.TryGetValue("query", out var q) ? q as string : null;
        var retrievedDoc = shared.TryGetValue("retrieved_document", out var rd) ? rd as Dictionary<string, object> : null;
        
        if (query == null || retrievedDoc == null)
            return Task.FromResult<(string, Dictionary<string, object>)?>(null);
        
        return Task.FromResult<(string, Dictionary<string, object>)?>((query, retrievedDoc));
    }

    public override Task<string?> Exec((string, Dictionary<string, object>)? inputs)
    {
        if (inputs == null)
            return Task.FromResult<string?>(null);
        
        var (query, retrievedDoc) = inputs.Value;
        var answer = MockCallLLM(query, retrievedDoc["text"] as string ?? "");
        return Task.FromResult<string?>(answer);
    }

    public override Task<string?> Post(TShared shared, (string, Dictionary<string, object>)? prepRes, string? execRes)
    {
        if (execRes != null)
        {
            shared["generated_answer"] = execRes;
            Console.WriteLine("\nGenerated Answer:");
            Console.WriteLine(execRes);
        }
        return Task.FromResult<string?>("default");
    }

    private string MockCallLLM(string query, string context)
    {
        return $"Based on the context provided, the answer to '{query}' is: {context.Substring(0, Math.Min(100, context.Length))}...";
    }
}

class Program
{
    static void Main(string[] args)
    {
        var texts = new List<string>
        {
            "Pocket Flow is a 100-line minimalist LLM framework. Lightweight: Just 100 lines. Zero bloat, zero dependencies, zero vendor lock-in. Expressive: Everything you love—(Multi-)Agents, Workflow, RAG, and more. Agentic Coding: Let AI Agents build Agents—10x productivity boost! To install, pip install pocketflow.",
            "NeurAlign M7 is a revolutionary non-invasive neural alignment device. Targeted magnetic resonance technology increases neuroplasticity. Clinical trials showed 72% improvement in PTSD treatment outcomes. Developed by Cortex Medical in 2024.",
            "The Velvet Revolution of Caldonia (1967-1968) ended Generalissimo Verak's 40-year rule. Led by poet Eliza Markovian through underground literary societies. First democratic elections held in March 1968.",
            "Q-Mesh is QuantumLeap Technologies' instantaneous data synchronization protocol. Utilizes directed acyclic graph consensus for 500,000 transactions per second. Adopted by three central banks.",
            "Harlow Institute's Mycelium Strain HI-271 removes 99.7% of PFAS from contaminated soil. Breaks down chemicals into non-toxic compounds within 60 days."
        };
        
        Console.WriteLine("=".PadRight(50, '='));
        Console.WriteLine("PocketFlow RAG Document Retrieval");
        Console.WriteLine("=".PadRight(50, '='));
        
        var query = args.Length > 0 ? string.Join(" ", args).Replace("--", "") : "How to install PocketFlow?";
        
        var shared = new Dictionary<string, object>
        {
            ["texts"] = texts,
            ["query"] = query
        };
        
        var chunkNode = new ChunkDocumentsNode();
        var embedNode = new EmbedDocumentsNode();
        var createIndexNode = new CreateIndexNode();
        
        chunkNode.On("default").To(embedNode);
        embedNode.On("default").To(createIndexNode);
        
        var offlineFlow = new Flow<TShared>(chunkNode);
        offlineFlow.Run(shared).Wait();
        
        Console.WriteLine();
        
        var embedQueryNode = new EmbedQueryNode();
        var retrieveNode = new RetrieveDocumentNode();
        var generateNode = new GenerateAnswerNode();
        
        embedQueryNode.On("default").To(retrieveNode);
        retrieveNode.On("default").To(generateNode);
        
        var onlineFlow = new Flow<TShared>(embedQueryNode);
        onlineFlow.Run(shared).Wait();
    }
}
