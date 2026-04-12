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
        var images = new[] { "cat.jpg", "dog.jpg", "bird.jpg" };
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

    public override Task<string> ExecItem(Dictionary<string, string> item)
    {
        var input = item["input"];
        var filter = item["filter"];
        
        Console.WriteLine($"[ProcessImageNode] Loading images/{input}...");
        
        var filteredImage = ApplyFilterMock(input, filter);
        
        Console.WriteLine($"[ProcessImageNode] Applying {filter} filter...");
        
        var outputPath = SaveImageMock(filteredImage, input, filter);
        
        Console.WriteLine($"[ProcessImageNode] Saved to {outputPath}");
        
        return Task.FromResult(outputPath);
    }

    public override Task<string?> Post(TShared shared, IEnumerable<Dictionary<string, string>>? prepRes, IList<string>? execRes)
    {
        Console.WriteLine($"\n[ProcessImageNode] Processed {execRes?.Count ?? 0} images");
        return Task.FromResult<string?>(null);
    }

    private static string ApplyFilterMock(string input, string filter)
    {
        return $"[Filtered image: {input} with {filter}]";
    }

    private static string SaveImageMock(string imageData, string input, string filter)
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
        Console.WriteLine("Processing images with filters...\n");

        var processNode = new ProcessImageNode(maxRetries: 3);
        var flow = new Flow<TShared>(processNode);
        await flow.Run(new Dictionary<string, object>());

        Console.WriteLine("\nAll images processed successfully!");
        Console.WriteLine("Check the 'output' directory for results.");
    }
}
