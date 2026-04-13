#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class ProcessImageNode : BatchNode<TShared, Dictionary<string, string>, string>
{
    public ProcessImageNode(IDictionary<string, object>? defaultParams = null, int maxRetries = 1, double wait = 0, bool enableParallel = false)
        : base(defaultParams, maxRetries, wait, enableParallel)
    {
    }

    public override Task<IEnumerable<Dictionary<string, string>>?> Prep(TShared shared)
    {
        var images = shared.TryGetValue("images", out var i) ? i as List<string> ?? new List<string>() : new List<string>();
        var filters = new[] { "grayscale", "blur", "sepia" };

        var items = new List<Dictionary<string, string>>();
        foreach (var img in images)
        {
            foreach (var f in filters)
            {
                items.Add(new Dictionary<string, string>
                {
                    ["input"] = img,
                    ["filter"] = f
                });
            }
        }

        return Task.FromResult<IEnumerable<Dictionary<string, string>>?>(items);
    }

    public override async Task<string> ExecItem(Dictionary<string, string> item)
    {
        var input = item["input"];
        var filter = item["filter"];
        
        Console.WriteLine($"Loading image: {input}");
        await Task.Delay(500);
        
        Console.WriteLine($"Applying {filter} filter...");
        await Task.Delay(500);
        
        var outputPath = SaveImageMock(input, filter);
        Console.WriteLine($"Saved: {outputPath}");
        
        return outputPath;
    }

    public override Task<string?> Post(TShared shared, IEnumerable<Dictionary<string, string>>? prepRes, IList<string>? execRes)
    {
        Console.WriteLine($"\nProcessed {execRes?.Count ?? 0} images");
        return Task.FromResult<string?>(null);
    }

    private static string SaveImageMock(string input, string filter)
    {
        Directory.CreateDirectory("output");
        var inputName = Path.GetFileNameWithoutExtension(input);
        var outputPath = Path.Combine("output", $"{inputName}_{filter}.jpg");
        return outputPath;
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Parallel Image Processor");
        Console.WriteLine("-".PadRight(30));
        
        var imagePaths = new List<string> { "images/bird.jpg", "images/cat.jpg", "images/dog.jpg" };
        
        foreach (var path in imagePaths)
        {
            Console.WriteLine($"- {path}");
        }
        
        var shared = new Dictionary<string, object>
        {
            ["images"] = imagePaths
        };
        
        Console.WriteLine("\nRunning sequential batch processing...");
        var seqNode = new ProcessImageNode(enableParallel: false);
        var seqFlow = new Flow<TShared>(seqNode);
        
        var startTime = DateTime.Now;
        await seqFlow.Run(shared);
        var seqTime = DateTime.Now - startTime;
        
        Console.WriteLine("\nRunning parallel batch processing...");
        shared["images"] = imagePaths;
        var parNode = new ProcessImageNode(enableParallel: true);
        var parFlow = new Flow<TShared>(parNode);
        
        startTime = DateTime.Now;
        await parFlow.Run(shared);
        var parTime = DateTime.Now - startTime;
        
        Console.WriteLine("\nTiming Results:");
        Console.WriteLine($"Sequential batch processing: {seqTime.TotalSeconds:F2} seconds");
        Console.WriteLine($"Parallel batch processing: {parTime.TotalSeconds:F2} seconds");
        if (parTime.TotalSeconds > 0)
            Console.WriteLine($"Speedup: {seqTime.TotalSeconds / parTime.TotalSeconds:F2}x");
        
        Console.WriteLine("\nProcessing complete! Check the output/ directory for results.");
    }
}
