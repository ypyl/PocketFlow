#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class TranslateTextNode : BatchNode<TShared, (string Text, string Language), Dictionary<string, string>>
{
    public TranslateTextNode(IDictionary<string, object>? defaultParams = null, int maxRetries = 1, double wait = 0, bool enableParallel = false)
        : base(defaultParams, maxRetries, wait, enableParallel)
    {
    }

    public override Task<IEnumerable<(string Text, string Language)>?> Prep(TShared shared)
    {
        var text = shared.TryGetValue("text", out var t) ? t as string ?? "(No text provided)" : "(No text provided)";
        var languages = shared.TryGetValue("languages", out var l) ? l as List<string> ?? new List<string>() : new List<string> { "Chinese", "Spanish", "Japanese", "German", "Russian", "Portuguese", "French", "Korean" };
        
        var items = languages.Select(lang => (text, lang)).ToList();
        return Task.FromResult<IEnumerable<(string, string)>?>(items);
    }

    public override Task<Dictionary<string, string>> ExecItem((string Text, string Language) item)
    {
        var (text, language) = item;
        
        var prompt = $@"Please translate the following markdown file into {language}. 
But keep the original markdown format, links and code blocks.
Directly return the translated text, without any other text or comments.

Original: 
{text}

Translated:";

        Console.WriteLine($"[TranslateTextNode] Translating to {language}...");
        var result = MockCallLLM(prompt, language);
        Console.WriteLine($"[TranslateTextNode] Translated {language} text");
        
        return Task.FromResult(new Dictionary<string, string> { ["language"] = language, ["translation"] = result });
    }

    public override Task<string?> Post(TShared shared, IEnumerable<(string Text, string Language)>? prepRes, IList<Dictionary<string, string>>? execRes)
    {
        var outputDir = shared.TryGetValue("output_dir", out var od) ? od as string ?? "translations" : "translations";
        
        Directory.CreateDirectory(outputDir);
        
        if (execRes == null) return Task.FromResult<string?>(null);
        
        foreach (var result in execRes)
        {
            var language = result["language"];
            var translation = result["translation"];
            
            var filename = Path.Combine(outputDir, $"README_{language.ToUpper()}.md");
            File.WriteAllText(filename, translation);
            
            Console.WriteLine($"[TranslateTextNode] Saved translation to {filename}");
        }
        
        return Task.FromResult<string?>(null);
    }

    private static string MockCallLLM(string prompt, string language)
    {
        return $"[Translation to {language}] This is a mock translation of the content. In a real application, this would call an LLM API to perform the actual translation while preserving markdown formatting, links, and code blocks.";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var text = "Welcome to PocketFlow!\n\nThis is a **sample text** that needs translation.\n\n```python\nprint('hello')\n```";
        
        var languages = new List<string> { "Chinese", "Spanish", "Japanese", "German" };
        
        var shared = new Dictionary<string, object>
        {
            ["text"] = text,
            ["languages"] = languages,
            ["output_dir"] = "cookbook.net/translations"
        };

        Console.WriteLine($"\nStarting sequential translation into {languages.Count} languages...\n");

        var startTime = DateTime.Now;
        
        var translateNode = new TranslateTextNode(null, 3);
        var flow = new Flow<TShared>(translateNode);
        await flow.Run(shared);
        
        var duration = (DateTime.Now - startTime).TotalSeconds;

        Console.WriteLine($"\nTotal sequential translation time: {duration:F4} seconds");
        Console.WriteLine("\n=== Translation Complete ===");
        Console.WriteLine($"Translations saved to: {shared["output_dir"]}");
        Console.WriteLine("============================");
    }
}
