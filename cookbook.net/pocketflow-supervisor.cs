#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class DecideAction : Node<TShared, (string Question, string Context)?, Dictionary<string, object>>
{
    public override Task<(string, string)?> Prep(TShared shared)
    {
        var context = shared.TryGetValue("context", out var c) ? c as string ?? "No previous search" : "No previous search";
        var question = shared.TryGetValue("question", out var q) ? q as string ?? "" : "";
        return Task.FromResult<(string, string)?>((question, context));
    }

    public override Task<Dictionary<string, object>?> Exec((string, string)? inputs)
    {
        if (inputs == null)
            return Task.FromResult<Dictionary<string, object>?>(null);
        
        var (question, context) = inputs.Value;
        
        Console.WriteLine("Agent deciding what to do next...");
        
        var decision = MockLLMDecision(question, context);
        return Task.FromResult<Dictionary<string, object>?>(decision);
    }

    public override Task<string?> Post(TShared shared, (string, string)? prepRes, Dictionary<string, object>? execRes)
    {
        if (execRes == null)
            return Task.FromResult<string?>("default");
        
        if (execRes["action"]?.ToString() == "search")
        {
            shared["search_query"] = execRes["search_query"] ?? "";
            Console.WriteLine($"Agent decided to search for: {execRes["search_query"]}");
        }
        else
        {
            Console.WriteLine("Agent decided to answer the question");
        }
        
        return Task.FromResult<string?>(execRes["action"]?.ToString());
    }

    private Dictionary<string, object> MockLLMDecision(string question, string context)
    {
        if (!context.Contains("SEARCH:"))
        {
            return new Dictionary<string, object>
            {
                ["action"] = "search",
                ["search_query"] = question
            };
        }
        
        return new Dictionary<string, object>
        {
            ["action"] = "answer",
            ["answer"] = "The Nobel Prize in Physics 2024 was awarded to John Hopfield and Geoffrey Hinton."
        };
    }
}

class SearchWeb : Node<TShared, string?, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        var searchQuery = shared.TryGetValue("search_query", out var q) ? q as string ?? "" : "";
        return Task.FromResult<string?>(searchQuery);
    }

    public override Task<string?> Exec(string? searchQuery)
    {
        if (searchQuery == null)
            return Task.FromResult<string?>(null);
        
        Console.WriteLine($"Searching the web for: {searchQuery}");
        var results = MockWebSearch(searchQuery);
        return Task.FromResult<string?>(results);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        var previous = shared.TryGetValue("context", out var c) ? c as string ?? "" : "";
        shared["context"] = previous + $"\n\nSEARCH: {shared["search_query"]}\nRESULTS: {execRes}";
        
        Console.WriteLine("Found information, analyzing results...");
        
        return Task.FromResult<string?>("decide");
    }

    private string MockWebSearch(string query)
    {
        return $"Mock results for '{query}': Nobel Prize 2024 Physics awarded to John Hopfield and Geoffrey Hinton for neural network foundations.";
    }
}

class UnreliableAnswerNode : Node<TShared, (string Question, string Context)?, string>
{
    private Random _random = new Random();

    public override Task<(string, string)?> Prep(TShared shared)
    {
        var question = shared.TryGetValue("question", out var q) ? q as string ?? "" : "";
        var context = shared.TryGetValue("context", out var c) ? c as string ?? "" : "";
        return Task.FromResult<(string, string)?>((question, context));
    }

    public override Task<string?> Exec((string, string)? inputs)
    {
        if (inputs == null)
            return Task.FromResult<string?>(null);
        
        var (question, context) = inputs.Value;
        
        if (_random.NextDouble() < 0.5)
        {
            Console.WriteLine("Generating unreliable dummy answer...");
            return Task.FromResult<string?>("Sorry, I'm on a coffee break. The answer is 42, or maybe purple unicorns!");
        }
        
        Console.WriteLine("Crafting final answer...");
        return Task.FromResult<string?>("Based on my research, the Nobel Prize in Physics 2024 was awarded to John Hopfield and Geoffrey Hinton.");
    }

    public override Task<string?> Post(TShared shared, (string, string)? prepRes, string? execRes)
    {
        shared["answer"] = execRes ?? "";
        Console.WriteLine("Answer generated successfully");
        return Task.FromResult<string?>("default");
    }
}

class SupervisorNode : Node<TShared, string?, Dictionary<string, object>>
{
    public override Task<string?> Prep(TShared shared)
    {
        var answer = shared.TryGetValue("answer", out var a) ? a as string ?? "" : "";
        return Task.FromResult<string?>(answer);
    }

    public override Task<Dictionary<string, object>?> Exec(string? answer)
    {
        Console.WriteLine("Supervisor checking answer quality...");
        
        var nonsenseMarkers = new[] { "coffee break", "purple unicorns", "made up", "42", "Who knows?" };
        
        var isNonsense = nonsenseMarkers.Any(marker => (answer ?? "").Contains(marker, StringComparison.OrdinalIgnoreCase));
        
        if (isNonsense)
        {
            return Task.FromResult<Dictionary<string, object>?>(new Dictionary<string, object>
            {
                ["valid"] = false,
                ["reason"] = "Answer appears to be nonsensical"
            });
        }
        
        return Task.FromResult<Dictionary<string, object>?>(new Dictionary<string, object>
        {
            ["valid"] = true,
            ["reason"] = "Answer appears to be legitimate"
        });
    }

    public override Task<string?> Post(TShared shared, string? prepRes, Dictionary<string, object>? execRes)
    {
        if (execRes == null)
            return Task.FromResult<string?>("default");
        
        if ((bool)execRes["valid"])
        {
            Console.WriteLine($"Supervisor approved: {execRes["reason"]}");
            return Task.FromResult<string?>("default");
        }
        
        Console.WriteLine($"Supervisor rejected: {execRes["reason"]}");
        shared["answer"] = null;
        var context = shared.TryGetValue("context", out var c) ? c as string ?? "" : "";
        shared["context"] = context + "\n\nNOTE: Previous answer rejected.";
        
        return Task.FromResult<string?>("retry");
    }
}

class Program
{
    static void Main(string[] args)
    {
        var question = args.Length > 0 ? string.Join(" ", args).Replace("--", "") : "Who won the Nobel Prize in Physics 2024?";
        
        Console.WriteLine($"Processing question: {question}\n");
        
        var decide = new DecideAction();
        var search = new SearchWeb();
        var answer = new UnreliableAnswerNode();
        var supervisor = new SupervisorNode();
        
        decide.On("search").To(search);
        decide.On("answer").To(answer);
        search.On("decide").To(decide);
        
        var agentFlow = new Flow<TShared>(decide);
        
        agentFlow.On("default").To(supervisor);
        supervisor.On("retry").To(agentFlow);
        
        var shared = new Dictionary<string, object>
        {
            ["question"] = question,
            ["context"] = "No previous search"
        };
        
        agentFlow.Run(shared).Wait();
        
        Console.WriteLine($"\nFinal Answer: {shared.GetValueOrDefault("answer", "No answer")}");
    }
}
