#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class EndNode : Node<TShared, string?, string>
{
    public override Task<string?> Prep(TShared shared)
        => Task.FromResult<string?>(null);
}

class TextInput : Node<TShared, string?, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        Console.Write("Enter text (or 'q' to quit): ");
        var input = Console.ReadLine() ?? "";
        return Task.FromResult<string?>(input);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        if (prepRes == "q")
            return Task.FromResult<string?>("exit");
        
        shared["text"] = prepRes ?? "";
        
        if (!shared.ContainsKey("stats"))
        {
            shared["stats"] = new Dictionary<string, object>
            {
                ["total_texts"] = 0,
                ["total_words"] = 0
            };
        }
        
        ((Dictionary<string, object>)shared["stats"])["total_texts"] = (int)((Dictionary<string, object>)shared["stats"])["total_texts"] + 1;
        
        return Task.FromResult<string?>("count");
    }
}

class WordCounter : Node<TShared, string?, int>
{
    public override Task<string?> Prep(TShared shared)
        => Task.FromResult<string?>(shared["text"] as string);

    public override Task<int> Exec(string? text)
    {
        var count = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
        return Task.FromResult(count);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, int execRes)
    {
        ((Dictionary<string, object>)shared["stats"])["total_words"] = (int)((Dictionary<string, object>)shared["stats"])["total_words"] + execRes;
        return Task.FromResult<string?>("show");
    }
}

class ShowStats : Node<TShared, Dictionary<string, object>, string>
{
    public override Task<Dictionary<string, object>?> Prep(TShared shared)
        => Task.FromResult<Dictionary<string, object>?>(shared["stats"] as Dictionary<string, object>);

    public override Task<string?> Post(TShared shared, Dictionary<string, object>? prepRes, string? execRes)
    {
        var stats = prepRes ?? new Dictionary<string, object>();
        var totalTexts = (int)stats["total_texts"];
        var totalWords = (int)stats["total_words"];
        
        Console.WriteLine($"\nStatistics:");
        Console.WriteLine($"- Texts processed: {totalTexts}");
        Console.WriteLine($"- Total words: {totalWords}");
        Console.WriteLine($"- Average words per text: {(totalTexts > 0 ? (double)totalWords / totalTexts : 0):F1}\n");
        
        return Task.FromResult<string?>("continue");
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var textInput = new TextInput();
        var wordCounter = new WordCounter();
        var showStats = new ShowStats();
        var endNode = new EndNode();
        
        textInput.On("count").To(wordCounter);
        wordCounter.On("show").To(showStats);
        showStats.On("continue").To(textInput);
        textInput.On("exit").To(endNode);
        
        var flow = new Flow<TShared>(textInput);
        await flow.Run(new Dictionary<string, object>());
    }
}
