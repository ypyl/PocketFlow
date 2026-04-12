#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class Summarize : Node<TShared, string, string>
{
    public Summarize(int maxRetries = 1, double wait = 0) : base(maxRetries, wait) { }

    public override Task<string?> Prep(TShared shared)
        => Task.FromResult<string?>(shared["data"] as string);

    public override Task<string?> Exec(string? prepRes)
    {
        if (string.IsNullOrEmpty(prepRes))
            return Task.FromResult<string?>("Empty text");

        var prompt = $"Summarize this text in 10 words: {prepRes}";
        Console.WriteLine($"[Summarize] Calling LLM with prompt: {prompt}");

        return Task.FromResult<string?>("[Mock summary from LLM]");
    }

    public override Task<string?> ExecFallback(string? prepRes, Exception exc)
    {
        Console.WriteLine($"[Summarize] LLM call failed: {exc.Message}");
        return Task.FromResult<string?>("There was an error processing your request.");
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        shared["summary"] = execRes!;
        return Task.FromResult<string?>(null);
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var text = """
        PocketFlow is a minimalist LLM framework that models workflows as a Nested Directed Graph.
        Nodes handle simple LLM tasks, connecting through Actions for Agents.
        Flows orchestrate these nodes for Task Decomposition, and can be nested.
        It also supports Batch processing and Async execution.
        """;

        var shared = new Dictionary<string, object> { ["data"] = text };

        Console.WriteLine("Running Summarize flow...\n");

        var flow = new Flow<TShared>(new Summarize(maxRetries: 3));
        await flow.Run(shared);

        Console.WriteLine("\nInput text:" + text);
        Console.WriteLine("\nSummary: " + shared["summary"]);
    }
}
