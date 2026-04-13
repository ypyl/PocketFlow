#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Text.RegularExpressions;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class ResumeParserNode : Node<TShared, Dictionary<string, object>, Dictionary<string, object>>
{
    public override Task<Dictionary<string, object>?> Prep(TShared shared)
    {
        var resumeText = shared.TryGetValue("resume_text", out var t) ? t as string ?? "" : "";
        var targetSkills = shared.TryGetValue("target_skills", out var s) ? s as List<string> ?? new List<string>() : new List<string>();
        
        return Task.FromResult<Dictionary<string, object>?>(new Dictionary<string, object>
        {
            ["resume_text"] = resumeText,
            ["target_skills"] = targetSkills
        });
    }

    public override Task<Dictionary<string, object>?> Exec(Dictionary<string, object>? prepRes)
    {
        if (prepRes == null)
            return Task.FromResult<Dictionary<string, object>?>(null);
        
        var resumeText = prepRes["resume_text"] as string ?? "";
        var targetSkills = prepRes["target_skills"] as List<string> ?? new List<string>();
        
        var structuredResult = MockExtractResume(resumeText, targetSkills);
        return Task.FromResult<Dictionary<string, object>?>(structuredResult);
    }

    public override Task<string?> Post(TShared shared, Dictionary<string, object>? prepRes, Dictionary<string, object>? execRes)
    {
        if (execRes != null)
        {
            shared["structured_data"] = execRes;
            
            Console.WriteLine("\n=== STRUCTURED RESUME DATA ===\n");
            Console.WriteLine($"name: {execRes.GetValueOrDefault("name", "N/A")}");
            Console.WriteLine($"email: {execRes.GetValueOrDefault("email", "N/A")}");
            
            var experience = execRes.GetValueOrDefault("experience") as List<object>;
            if (experience != null)
            {
                Console.WriteLine("experience:");
                foreach (var exp in experience)
                {
                    if (exp is Dictionary<string, object> expDict)
                    {
                        Console.WriteLine($"  - title: {expDict.GetValueOrDefault("title", "N/A")}");
                        Console.WriteLine($"    company: {expDict.GetValueOrDefault("company", "N/A")}");
                    }
                }
            }
            
            var skillIndexes = execRes.GetValueOrDefault("skill_indexes") as List<object>;
            if (skillIndexes != null)
            {
                Console.WriteLine($"skill_indexes: [{string.Join(", ", skillIndexes)}]");
            }
            
            Console.WriteLine("\n================================");
            Console.WriteLine("Extracted resume information.");
        }
        
        return Task.FromResult<string?>("default");
    }

    private Dictionary<string, object> MockExtractResume(string resumeText, List<string> targetSkills)
    {
        var nameMatch = Regex.Match(resumeText, @"^([A-Z][A-Za-z]+ [A-Z][A-Za-z]+)", RegexOptions.Multiline);
        var emailMatch = Regex.Match(resumeText, @"[\w\.-]+@[\w\.-]+\.\w+");
        
        var result = new Dictionary<string, object>
        {
            ["name"] = nameMatch.Success ? nameMatch.Value : "Unknown",
            ["email"] = emailMatch.Success ? emailMatch.Value : "No email found"
        };
        
        var experience = new List<Dictionary<string, string>>
        {
            new Dictionary<string, string> { ["title"] = "SALES MANAGER", ["company"] = "ABC Corporation" },
            new Dictionary<string, string> { ["title"] = "ASST. MANAGER", ["company"] = "XYZ Industries" }
        };
        result["experience"] = experience;
        
        var skillIndexes = new List<int> { 0, 1, 2, 3, 4 };
        result["skill_indexes"] = skillIndexes;
        
        return result;
    }
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Resume Parser - Structured Output ===\n");
        
        var targetSkills = new List<string>
        {
            "Team leadership & management",
            "CRM software",
            "Project management",
            "Public speaking",
            "Microsoft Office",
            "Python",
            "Data Analysis"
        };
        
        var resumeText = @"JOHN SMITH
johnsmith1983@gmail.com

EXPERIENCE

SALES MANAGER at ABC Corporation
Managed team of 10 sales representatives. Increased revenue by 25%.

ASST. MANAGER at XYZ Industries
Assisted with daily operations. Implemented new CRM software.

SKILLS
Team leadership & management, CRM software, Project management, Public speaking, Microsoft Office";
        
        var shared = new Dictionary<string, object>
        {
            ["resume_text"] = resumeText,
            ["target_skills"] = targetSkills
        };
        
        var parserNode = new ResumeParserNode();
        var flow = new Flow<TShared>(parserNode);
        flow.Run(shared).Wait();
        
        if (shared.TryGetValue("structured_data", out var dataObj) && dataObj is Dictionary<string, object> data)
        {
            Console.WriteLine("\n--- Found Target Skills (from Indexes) ---");
            var skillIndexes = data.GetValueOrDefault("skill_indexes") as List<object>;
            if (skillIndexes != null)
            {
                foreach (var idx in skillIndexes)
                {
                    if (idx is int index && index >= 0 && index < targetSkills.Count)
                    {
                        Console.WriteLine($"- {targetSkills[index]} (Index: {index})");
                    }
                }
            }
            Console.WriteLine("----------------------------------------\n");
        }
    }
}
