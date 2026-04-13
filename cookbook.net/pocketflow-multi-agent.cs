#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class HinterNode : Node<TShared, (string Target, List<string> Forbidden, List<string> PastGuesses)?, string>
{
    public override Task<(string, List<string>, List<string>)?> Prep(TShared shared)
    {
        if (!shared.ContainsKey("hinter_turn"))
            shared["hinter_turn"] = false;
        
        var guesserTurn = (bool)shared["hinter_turn"];
        if (guesserTurn)
        {
            return Task.FromResult<(string, List<string>, List<string>)?>(null);
        }
        
        shared["hinter_turn"] = true;
        
        var target = shared.TryGetValue("target_word", out var t) ? t as string ?? "" : "";
        var forbidden = shared.TryGetValue("forbidden_words", out var f) ? f as List<string> ?? new List<string>() : new List<string>();
        var pastGuesses = shared.TryGetValue("past_guesses", out var pg) ? pg as List<string> ?? new List<string>() : new List<string>();
        
        return Task.FromResult<(string, List<string>, List<string>)?>((target, forbidden, pastGuesses));
    }

    public override Task<string?> Exec((string, List<string>, List<string>)? inputs)
    {
        if (inputs == null)
            return Task.FromResult<string?>(null);
        
        var (target, forbidden, pastGuesses) = inputs.Value;
        
        Console.WriteLine($"[Hinter] Target: {target}, Forbidden: {string.Join(", ", forbidden)}");
        
        var hint = MockCallLLM($"Generate hint for '{target}'. Forbidden: {string.Join(", ", forbidden)}");
        if (pastGuesses.Count > 0)
            hint += " (more specific since wrong guesses: " + string.Join(", ", pastGuesses) + ")";
        
        Console.WriteLine($"\nHinter: Here's your hint - {hint}");
        return Task.FromResult<string?>(hint);
    }

    public override Task<string?> Post(TShared shared, (string, List<string>, List<string>)? prepRes, string? execRes)
    {
        if (execRes == null)
        {
            shared["game_over"] = true;
            return Task.FromResult<string?>("end");
        }
        
        shared["current_hint"] = execRes;
        return Task.FromResult<string?>("continue");
    }

    private string MockCallLLM(string prompt)
    {
        if (prompt.Contains("nostalgic"))
            return "This feeling reminds me of childhood days";
        if (prompt.Contains("elephant"))
            return "Large gray animal with memory";
        return "A common English word";
    }
}

class GuesserNode : Node<TShared, string?, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        if (!shared.ContainsKey("hinter_turn"))
            shared["hinter_turn"] = false;
        
        var hinterTurn = (bool)shared["hinter_turn"];
        if (!hinterTurn)
        {
            return Task.FromResult<string?>(null);
        }
        
        shared["hinter_turn"] = false;
        
        var hint = shared.TryGetValue("current_hint", out var h) ? h as string ?? "" : "";
        return Task.FromResult<string?>(hint);
    }

    public override Task<string?> Exec(string? hint)
    {
        if (hint == null)
            return Task.FromResult<string?>(null);
        
        var guess = MockCallLLM($"Given hint: {hint}, make a new guess. Reply a single word:");
        Console.WriteLine($"Guesser: I guess it's - {guess}");
        return Task.FromResult<string?>(guess);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        if (execRes == null)
            return Task.FromResult<string?>("end");
        
        var target = shared.TryGetValue("target_word", out var t) ? t as string ?? "" : "";
        
        if (execRes.Equals(target, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Game Over - Correct guess!");
            shared["game_over"] = true;
            return Task.FromResult<string?>("end");
        }
        
        if (!shared.ContainsKey("past_guesses"))
            shared["past_guesses"] = new List<string>();
        ((List<string>)shared["past_guesses"]).Add(execRes);
        
        return Task.FromResult<string?>("continue");
    }

    private string MockCallLLM(string prompt)
    {
        if (prompt.Contains("nostalgic") || prompt.Contains("childhood"))
            return "nostalgic";
        if (prompt.Contains("memory"))
            return "memory";
        if (prompt.Contains("elephant") || prompt.Contains("large"))
            return "elephant";
        return "wrong";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var shared = new Dictionary<string, object>
        {
            ["target_word"] = "nostalgic",
            ["forbidden_words"] = new List<string> { "memory", "past", "remember", "feeling", "longing" },
            ["hinter_turn"] = false,
            ["past_guesses"] = new List<string>(),
            ["current_hint"] = "",
            ["game_over"] = false
        };

        Console.WriteLine("=========== Taboo Game Starting! ===========");
        Console.WriteLine($"Target word: {shared["target_word"]}");
        Console.WriteLine($"Forbidden words: {string.Join(", ", (List<string>)shared["forbidden_words"])}");
        Console.WriteLine("============================================");

        var hinter = new HinterNode();
        var guesser = new GuesserNode();

        hinter.On("continue").To(guesser);
        guesser.On("continue").To(hinter);
        hinter.On("end").To(null!);
        guesser.On("end").To(null!);

        var flow = new Flow<TShared>(hinter);
        
        int maxTurns = 20;
        int turn = 0;
        
        while (!(bool)(shared["game_over"]) && turn < maxTurns)
        {
            turn++;
            Console.WriteLine($"\n--- Turn {turn} ---");
            await flow.Run(shared);
        }

        Console.WriteLine("\n=========== Game Complete! ===========");
    }
}
