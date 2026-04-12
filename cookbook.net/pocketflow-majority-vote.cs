#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Text.RegularExpressions;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class MajorityVoteNode : BatchNode<TShared, string, string>
{
    public MajorityVoteNode(IDictionary<string, object>? defaultParams = null, int maxRetries = 1, double wait = 0, bool enableParallel = false)
        : base(defaultParams, maxRetries, wait, enableParallel)
    {
    }

    public override Task<IEnumerable<string>?> Prep(TShared shared)
    {
        var question = shared.TryGetValue("question", out var q) ? q as string ?? "(No question provided)" : "(No question provided)";
        var attemptsCount = shared.TryGetValue("num_tries", out var n) ? (int)n : 3;
        
        return Task.FromResult<IEnumerable<string>?>(Enumerable.Repeat(question, attemptsCount));
    }

    public override Task<string> ExecItem(string question)
    {
        Console.WriteLine($"[MajorityVoteNode] Running attempt for: {question.Substring(0, Math.Min(50, question.Length))}...");
        var response = MockCallLLM(question);
        var answer = ParseYamlResponse(response);
        return Task.FromResult(answer);
    }

    public override Task<string> ExecFallback(string item, Exception exc)
    {
        Console.WriteLine($"[MajorityVoteNode] Fallback triggered: {exc.Message}");
        return Task.FromResult<string>(null!);
    }

    public override Task<string?> Post(TShared shared, IEnumerable<string>? prepRes, IList<string>? execResList)
    {
        var validResults = execResList?.Where(r => r != null).ToList() ?? new List<string>();
        
        var counter = validResults.GroupBy(x => x).OrderByDescending(g => g.Count()).ToList();
        var bestAnswer = counter.FirstOrDefault()?.Key ?? "No valid answer";
        var freq = counter.FirstOrDefault()?.Count() ?? 0;

        shared["majority_answer"] = bestAnswer;

        Console.WriteLine("========================");
        Console.WriteLine("All structured answers: " + string.Join(", ", validResults));
        Console.WriteLine("Majority vote => " + bestAnswer);
        Console.WriteLine("Frequency => " + freq);
        Console.WriteLine("========================");

        return Task.FromResult<string?>("end");
    }

    private string MockCallLLM(string question)
    {
        return @"thinking: |
    This is a complex probability problem.
    Let me work through the math carefully.
answer: 0.125";
    }

    private string ParseYamlResponse(string response)
    {
        var yamlMatch = Regex.Match(response, @"answer:\s*(.+)");
        if (yamlMatch.Success)
        {
            return yamlMatch.Groups[1].Value.Trim();
        }
        return "0.000";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var problem = @"You work at a shoe factory. In front of you, there are three pairs of shoes (six individual shoes) with the following sizes: two size 4s, two size 5s, and two size 6s. The factory defines an 'acceptable pair' as two shoes that differ in size by a maximum of one size. If you close your eyes and randomly pick three pairs of shoes without replacement, what is the probability that you end up drawing three acceptable pairs?";
        
        var numTries = 5;

        var shared = new Dictionary<string, object>
        {
            ["question"] = problem,
            ["num_tries"] = numTries
        };

        Console.WriteLine("Starting Majority Vote Reasoning...\n");

        var majorityNode = new MajorityVoteNode();
        var flow = new Flow<TShared>(majorityNode);
        await flow.Run(shared);

        Console.WriteLine("\n=== Final Answer ===");
        Console.WriteLine(shared["majority_answer"]);
        Console.WriteLine("====================");
    }
}
