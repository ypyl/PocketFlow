#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class LoadGrades : Node<TShared, string?, List<double>>
{
    public override Task<string?> Prep(TShared shared)
    {
        var className = Params.TryGetValue("class", out var c) ? c as string ?? "" : "";
        var studentFile = Params.TryGetValue("student", out var s) ? s as string ?? "" : "";
        var filePath = Path.Combine("school", className, studentFile);
        return Task.FromResult<string?>(filePath);
    }

    public override Task<List<double>?> Exec(string? filePath)
    {
        if (filePath == null)
            return Task.FromResult<List<double>?>(null);
        
        var grades = File.ReadAllLines(filePath)
            .Select(line => double.Parse(line.Trim()))
            .ToList();
        return Task.FromResult<List<double>?>(grades);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, List<double>? grades)
    {
        if (grades != null)
            shared["grades"] = grades;
        return Task.FromResult<string?>("calculate");
    }
}

class CalculateAverage : Node<TShared, List<double>?, double?>
{
    public override Task<List<double>?> Prep(TShared shared)
    {
        if (shared.TryGetValue("grades", out var grades))
            return Task.FromResult(grades as List<double>);
        return Task.FromResult<List<double>?>(null);
    }

    public override Task<double?> Exec(List<double>? grades)
    {
        if (grades == null || grades.Count == 0)
            return Task.FromResult<double?>(null);
        
        var average = grades.Average();
        return Task.FromResult<double?>(average);
    }

    public override Task<string?> Post(TShared shared, List<double>? prepRes, double? average)
    {
        if (!average.HasValue)
            return Task.FromResult<string?>("default");
        
        if (!shared.ContainsKey("results"))
            shared["results"] = new Dictionary<string, Dictionary<string, double>>();
        
        var className = Params.TryGetValue("class", out var c) ? c as string ?? "" : "";
        var student = Params.TryGetValue("student", out var s) ? s as string ?? "" : "";
        
        var results = (Dictionary<string, Dictionary<string, double>>)shared["results"];
        if (!results.ContainsKey(className))
            results[className] = new Dictionary<string, double>();
        
        results[className][student] = average.Value;
        
        Console.WriteLine($"- {student}: Average = {average.Value:F1}");
        return Task.FromResult<string?>("default");
    }
}

class Program
{
    static void CreateSampleData()
    {
        Directory.CreateDirectory(Path.Combine("school", "class_a"));
        Directory.CreateDirectory(Path.Combine("school", "class_b"));
        
        File.WriteAllLines(Path.Combine("school", "class_a", "student1.txt"), new[] { "7.5", "8.0", "9.0" });
        File.WriteAllLines(Path.Combine("school", "class_a", "student2.txt"), new[] { "8.5", "7.0", "9.5" });
        File.WriteAllLines(Path.Combine("school", "class_b", "student3.txt"), new[] { "6.5", "8.5", "7.0" });
        File.WriteAllLines(Path.Combine("school", "class_b", "student4.txt"), new[] { "9.0", "9.5", "8.0" });
    }
    
    static async Task Main(string[] args)
    {
        CreateSampleData();
        
        Console.WriteLine("Processing school grades...\n");
        
        var shared = new Dictionary<string, object>();
        
        if (!Directory.Exists("school"))
        {
            Console.WriteLine("No school directory found.");
            return;
        }
        
        var classDirs = Directory.GetDirectories("school");
        
        foreach (var classDir in classDirs)
        {
            var className = Path.GetFileName(classDir);
            Console.WriteLine($"Processing {className}...");
            
            var studentFiles = Directory.GetFiles(classDir, "*.txt");
            
            foreach (var studentFile in studentFiles)
            {
                var studentName = Path.GetFileName(studentFile);
                
                var loadGrades = new LoadGrades();
                var calcAverage = new CalculateAverage();
                
                loadGrades.SetParams(new Dictionary<string, object>
                {
                    ["class"] = className,
                    ["student"] = studentName
                });
                calcAverage.SetParams(new Dictionary<string, object>
                {
                    ["class"] = className,
                    ["student"] = studentName
                });
                
                loadGrades.On("calculate").To(calcAverage);
                
                var flow = new Flow<TShared>(loadGrades);
                await flow.Run(shared);
            }
            
            if (shared.TryGetValue("results", out var resultsObj) && 
                resultsObj is Dictionary<string, Dictionary<string, double>> results &&
                results.TryGetValue(className, out var classResults))
            {
                var classAverage = classResults.Values.Average();
                Console.WriteLine($"Class {className.Split('_')[1].ToUpper()} Average: {classAverage:F2}\n");
            }
        }
        
        if (shared.TryGetValue("results", out var finalResultsObj) && 
            finalResultsObj is Dictionary<string, Dictionary<string, double>> finalResults)
        {
            var allGrades = finalResults.Values.SelectMany(r => r.Values).ToList();
            if (allGrades.Count > 0)
            {
                var schoolAverage = allGrades.Average();
                Console.WriteLine($"School Average: {schoolAverage:F2}");
            }
        }
        
        Console.WriteLine("\nFinal results: " + (shared.ContainsKey("results") ? "stored" : "NOT stored"));
    }
}
