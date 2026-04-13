#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Diagnostics;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class GreetingNode : Node<TShared, string, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        var name = shared.TryGetValue("name", out var n) ? n as string ?? "World" : "World";
        return Task.FromResult<string?>(name);
    }

    public override Task<string> Exec(string name)
    {
        Console.WriteLine($"[GreetingNode] Creating greeting for {name}");
        return Task.FromResult($"Hello, {name}!");
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string execRes)
    {
        shared["greeting"] = execRes;
        return Task.FromResult<string?>("default");
    }
}

class UppercaseNode : Node<TShared, string, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        var greeting = shared.TryGetValue("greeting", out var g) ? g as string ?? "" : "";
        return Task.FromResult<string?>(greeting);
    }

    public override Task<string> Exec(string greeting)
    {
        Console.WriteLine("[UppercaseNode] Converting to uppercase");
        return Task.FromResult(greeting.ToUpperInvariant());
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string execRes)
    {
        shared["uppercase_greeting"] = execRes;
        Console.WriteLine($"[UppercaseNode] Stored uppercase greeting: {execRes}");
        return Task.FromResult<string?>("default");
    }
}

class BasicGreetingFlow : Flow<TShared>
{
    public BasicGreetingFlow()
    {
        var greetingNode = new GreetingNode();
        var uppercaseNode = new UppercaseNode();
        
        greetingNode >> uppercaseNode;
        
        base.SetStartNode(greetingNode);
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting PocketFlow Tracing Basic Example");
        Console.WriteLine("==================================================");

        var flow = new BasicGreetingFlow();

        var shared = new Dictionary<string, object>
        {
            ["name"] = "PocketFlow User"
        };

        Console.WriteLine($"Input: {shared["name"]}");

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await flow.RunAsync(shared);
            
            stopwatch.Stop();
            
            Console.WriteLine($"Output: {shared["greeting"]}");
            Console.WriteLine($"Final greeting: {shared["uppercase_greeting"]}");
            Console.WriteLine($"Flow completed successfully in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine("\nNote: In a production environment, this flow would be traced using Langfuse or similar observability tools.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Flow failed with error: {e.Message}");
            throw;
        }
    }
}
