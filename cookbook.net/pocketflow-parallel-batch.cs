#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class TranslateTextNodeParallel : BatchNode<TShared, (string Text, string Language), Dictionary<string, string>>
{
    public TranslateTextNodeParallel(IDictionary<string, object>? defaultParams = null, int maxRetries = 1, double wait = 0, bool enableParallel = false)
        : base(defaultParams, maxRetries, wait, enableParallel)
    {
    }

    public override Task<IEnumerable<(string, string)>?> Prep(TShared shared)
    {
        var text = shared.TryGetValue("text", out var t) ? t as string ?? "(No text provided)" : "(No text provided)";
        var languages = shared.TryGetValue("languages", out var l) ? l as List<string> ?? new List<string>() : new List<string>();
        
        var items = languages.Select(lang => (text, lang)).ToList();
        return Task.FromResult<IEnumerable<(string, string)>?>(items);
    }

    public override Task<Dictionary<string, string>> ExecItem((string Text, string Language) item)
    {
        var (text, language) = item;
        
        var prompt = $"Translate the following text into {language}. Keep markdown format. Return only the translation.\n\nOriginal: {text}\n\nTranslated ({language}):";
        var translation = MockCallLLM(prompt, language);
        
        Console.WriteLine($"[Parallel] Translated {language} text");
        return Task.FromResult(new Dictionary<string, string> { ["language"] = language, ["translation"] = translation });
    }

    public override Task<string?> Post(TShared shared, IEnumerable<(string Text, string Language)>? prepRes, IList<Dictionary<string, string>>? execRes)
    {
        var outputDir = shared.TryGetValue("output_dir", out var o) ? o as string ?? "translations" : "translations";
        
        Directory.CreateDirectory(outputDir);
        
        if (execRes != null)
        {
            foreach (var result in execRes)
            {
                if (result.TryGetValue("language", out var lang) && result.TryGetValue("translation", out var translation))
                {
                    var filename = Path.Combine(outputDir, $"README_{lang.ToString()?.ToUpper()}.md");
                    File.WriteAllText(filename, translation);
                    Console.WriteLine($"[Parallel] Saved translation to {filename}");
                }
            }
        }
        
        return Task.FromResult<string?>("default");
    }

    private string MockCallLLM(string prompt, string language)
    {
        return $"[Translation to {language}]: This is a mock translation of the original text. In a real implementation, this would be the actual translated content from an LLM API.";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var text = "# PocketFlow\n\nA Python library for building AI applications with directed graphs.";
        
        var languages = new List<string> { "Chinese", "Spanish", "Japanese", "German", "Russian", "Portuguese", "French", "Korean" };
        
        var shared = new Dictionary<string, object>
        {
            ["text"] = text,
            ["languages"] = languages,
            ["output_dir"] = "translations"
        };
        
        Console.WriteLine($"Starting parallel translation into {languages.Count} languages...\n");
        
        var translateNode = new TranslateTextNodeParallel(enableParallel: true);
        var flow = new Flow<TShared>(translateNode);
        
        var startTime = DateTime.Now;
        
        await flow.Run(shared);
        
        var duration = DateTime.Now - startTime;
        Console.WriteLine($"\nTotal parallel translation time: {duration.TotalSeconds:F4} seconds");
        Console.WriteLine("\n=== Translation Complete ===");
        Console.WriteLine($"Translations saved to: {shared["output_dir"]}");
        Console.WriteLine("============================");
    }
}
