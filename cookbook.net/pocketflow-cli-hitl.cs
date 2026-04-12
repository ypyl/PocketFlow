#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class GetTopicNode : Node<TShared, string?, string>
{
    public override Task<string?> Prep(TShared shared)
        => Task.FromResult<string?>(null);

    public override Task<string?> Exec(string? prepRes)
    {
        Console.Write("What topic would you like a joke about? ");
        var topic = Console.ReadLine() ?? "";
        return Task.FromResult<string?>(topic);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        shared["topic"] = execRes ?? "";
        return Task.FromResult<string?>("default");
    }
}

class GenerateJokeNode : Node<TShared, string, string>
{
    public override Task<string> Prep(TShared shared)
    {
        var topic = shared.TryGetValue("topic", out var t) ? t as string ?? "anything" : "anything";
        var dislikedJokes = shared.TryGetValue("disliked_jokes", out var d) ? d as List<string> ?? new List<string>() : new List<string>();
        
        var prompt = $"Please generate an one-liner joke about: {topic}. Make it short and funny.";
        if (dislikedJokes.Count > 0)
        {
            var dislikedStr = string.Join("; ", dislikedJokes);
            prompt = $"The user did not like the following jokes: [{dislikedStr}]. Please generate a new, different joke about {topic}.";
        }
        
        return Task.FromResult(prompt);
    }

    public override Task<string?> Exec(string prepRes)
    {
        Console.WriteLine("[GenerateJokeNode] Generating joke...");
        return Task.FromResult<string?>(MockCallLLM(prepRes));
    }

    public override Task<string?> Post(TShared shared, string prepRes, string? execRes)
    {
        shared["current_joke"] = execRes ?? "";
        Console.WriteLine($"\nJoke: {execRes}");
        return Task.FromResult<string?>("default");
    }

    private string MockCallLLM(string prompt)
    {
        if (prompt.Contains("anything"))
            return "Why don't scientists trust atoms? Because they make up everything!";
        if (prompt.ToLower().Contains("computer") || prompt.ToLower().Contains("code"))
            return "Why do programmers prefer dark mode? Because light attracts bugs!";
        if (prompt.ToLower().Contains("coffee"))
            return "Why did the coffee file a police report? It got mugged!";
        return $"Here's a joke about {prompt.Split(':').LastOrDefault()?.Trim() ?? "life"}: Why did the {prompt.Split(':').LastOrDefault()?.Trim() ?? "developer"} cross the road? To get to the other side!";
    }
}

class GetFeedbackNode : Node<TShared, string?, string>
{
    public override Task<string?> Prep(TShared shared)
        => Task.FromResult<string?>(null);

    public override Task<string?> Exec(string? prepRes)
    {
        while (true)
        {
            Console.Write("Did you like this joke? (yes/no): ");
            var feedback = (Console.ReadLine() ?? "").Trim().ToLower();
            if (feedback is "yes" or "y" or "no" or "n")
                return Task.FromResult<string?>(feedback);
            Console.WriteLine("Invalid input. Please type 'yes' or 'no'.");
        }
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        if (execRes is "yes" or "y")
        {
            shared["user_feedback"] = "approve";
            Console.WriteLine("Great! Glad you liked it.");
            return Task.FromResult<string?>("Approve");
        }
        else
        {
            shared["user_feedback"] = "disapprove";
            if (shared.TryGetValue("current_joke", out var joke) && joke is string currentJoke)
            {
                if (!shared.ContainsKey("disliked_jokes"))
                    shared["disliked_jokes"] = new List<string>();
                ((List<string>)shared["disliked_jokes"]).Add(currentJoke);
            }
            Console.WriteLine("Okay, let me try another one.");
            return Task.FromResult<string?>("Disapprove");
        }
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Welcome to the Command-Line Joke Generator!");

        var getTopic = new GetTopicNode();
        var generateJoke = new GenerateJokeNode();
        var getFeedback = new GetFeedbackNode();

        getTopic.On("default").To(generateJoke);
        generateJoke.On("default").To(getFeedback);
        getFeedback.On("Disapprove").To(generateJoke);

        var flow = new Flow<TShared>(getTopic);
        await flow.Run(new Dictionary<string, object>());

        Console.WriteLine("\nThanks for using the Joke Generator!");
    }
}
