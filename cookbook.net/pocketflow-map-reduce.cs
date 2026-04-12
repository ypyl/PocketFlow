#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Text.RegularExpressions;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class ReadResumesNode : Node<TShared, string?, Dictionary<string, string>>
{
    public override Task<string?> Prep(TShared shared)
        => Task.FromResult<string?>(null);

    public override Task<Dictionary<string, string>?> Exec(string? prepRes)
    {
        Console.WriteLine("[ReadResumesNode] Reading resumes...");
        
        var dataDir = Path.Combine("cookbook", "pocketflow-map-reduce", "data");
        var resumes = new Dictionary<string, string>();
        
        if (Directory.Exists(dataDir))
        {
            foreach (var file in Directory.GetFiles(dataDir, "*.txt"))
            {
                var filename = Path.GetFileName(file);
                resumes[filename] = File.ReadAllText(file);
            }
        }
        else
        {
            resumes["candidate1.txt"] = "John Doe - Bachelor's in Computer Science, 5 years experience, strong Python and Java skills.";
            resumes["candidate2.txt"] = "Jane Smith - Master's in Engineering, 2 years experience, knows SQL and Excel.";
            resumes["candidate3.txt"] = "Bob Johnson - PhD in Mathematics, 10 years at Google, expert in algorithms.";
        }
        
        Console.WriteLine($"[ReadResumesNode] Read {resumes.Count} resumes");
        return Task.FromResult<Dictionary<string, string>?>(resumes);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, Dictionary<string, string>? execRes)
    {
        shared["resumes"] = execRes ?? new Dictionary<string, string>();
        return Task.FromResult<string?>("default");
    }
}

class EvaluateResumesNode : BatchNode<TShared, KeyValuePair<string, string>, (string Filename, Dictionary<string, object> Result)>
{
    public EvaluateResumesNode(IDictionary<string, object>? defaultParams = null, int maxRetries = 1, double wait = 0, bool enableParallel = false)
        : base(defaultParams, maxRetries, wait, enableParallel)
    {
    }

    public override Task<IEnumerable<KeyValuePair<string, string>>?> Prep(TShared shared)
    {
        var resumes = shared["resumes"] as Dictionary<string, string> ?? new Dictionary<string, string>();
        return Task.FromResult<IEnumerable<KeyValuePair<string, string>>?>(resumes.ToList());
    }

    public override Task<(string Filename, Dictionary<string, object> Result)> ExecItem(KeyValuePair<string, string> resume)
    {
        Console.WriteLine($"[EvaluateResumesNode] Evaluating {resume.Key}...");
        
        var result = MockEvaluateResume(resume.Value);
        return Task.FromResult((resume.Key, result));
    }

    public override Task<string?> Post(TShared shared, IEnumerable<KeyValuePair<string, string>>? prepRes, IList<(string Filename, Dictionary<string, object> Result)>? execResList)
    {
        var evaluations = new Dictionary<string, Dictionary<string, object>>();
        if (execResList != null)
        {
            foreach (var (filename, result) in execResList)
            {
                evaluations[filename] = result;
            }
        }
        
        shared["evaluations"] = evaluations;
        return Task.FromResult<string?>("default");
    }

    private Dictionary<string, object> MockEvaluateResume(string content)
    {
        var nameMatch = Regex.Match(content, @"^(\w+\s?\w+)", RegexOptions.Multiline);
        var name = nameMatch.Success ? nameMatch.Groups[1].Value : "Unknown";
        
        var hasDegree = content.ToLower().Contains("bachelor") || content.ToLower().Contains("master") || content.ToLower().Contains("phd");
        var hasExperience = Regex.IsMatch(content, @"\d+\s*years?", RegexOptions.IgnoreCase);
        var yearsMatch = Regex.Match(content, @"(\d+)\s*years?", RegexOptions.IgnoreCase);
        var years = yearsMatch.Success ? int.Parse(yearsMatch.Groups[1].Value) : 0;
        
        var qualifies = hasDegree && years >= 3;
        
        return new Dictionary<string, object>
        {
            ["candidate_name"] = name,
            ["qualifies"] = qualifies,
            ["reasons"] = new List<string>
            {
                qualifies ? "Meets all criteria" : "Does not meet minimum experience requirement"
            }
        };
    }
}

class ReduceResultsNode : Node<TShared, Dictionary<string, Dictionary<string, object>>, Dictionary<string, object>>
{
    public override Task<Dictionary<string, Dictionary<string, object>>?> Prep(TShared shared)
        => Task.FromResult<Dictionary<string, Dictionary<string, object>>?>(shared["evaluations"] as Dictionary<string, Dictionary<string, object>>);

    public override Task<Dictionary<string, object>?> Exec(Dictionary<string, Dictionary<string, object>>? evaluations)
    {
        var qualifiedCount = 0;
        var totalCount = evaluations?.Count ?? 0;
        var qualifiedNames = new List<string>();
        
        if (evaluations != null)
        {
            foreach (var evaluation in evaluations.Values)
            {
                if (evaluation.TryGetValue("qualifies", out var q) && q is true)
                {
                    qualifiedCount++;
                    var name = evaluation.TryGetValue("candidate_name", out var n) ? n?.ToString() ?? "Unknown" : "Unknown";
                    qualifiedNames.Add(name);
                }
            }
        }
        
        return Task.FromResult<Dictionary<string, object>?>(new Dictionary<string, object>
        {
            ["total_candidates"] = totalCount,
            ["qualified_count"] = qualifiedCount,
            ["qualified_percentage"] = totalCount > 0 ? Math.Round((double)qualifiedCount / totalCount * 100, 1) : 0,
            ["qualified_names"] = qualifiedNames
        });
    }

    public override Task<string?> Post(TShared shared, Dictionary<string, Dictionary<string, object>>? prepRes, Dictionary<string, object>? execRes)
    {
        shared["summary"] = execRes;
        
        Console.WriteLine("\n===== Resume Qualification Summary =====");
        Console.WriteLine($"Total candidates evaluated: {execRes?["total_candidates"]}");
        Console.WriteLine($"Qualified candidates: {execRes?["qualified_count"]} ({execRes?["qualified_percentage"]}%)");
        
        var names = execRes?["qualified_names"] as List<string>;
        if (names?.Count > 0)
        {
            Console.WriteLine("\nQualified candidates:");
            foreach (var name in names)
                Console.WriteLine($"- {name}");
        }
        
        return Task.FromResult<string?>("default");
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting resume qualification processing...\n");

        var readResumes = new ReadResumesNode();
        var evaluateResumes = new EvaluateResumesNode();
        var reduceResults = new ReduceResultsNode();
        
        readResumes.On("default").To(evaluateResumes);
        evaluateResumes.On("default").To(reduceResults);

        var flow = new Flow<TShared>(readResumes);
        await flow.Run(new Dictionary<string, object>());

        Console.WriteLine("\nResume processing complete!");
    }
}
