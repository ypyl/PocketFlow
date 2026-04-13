#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Text;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class SearchNode : Node<TShared, (string Query, int NumResults), List<Dictionary<string, string>>>
{
    public override Task<(string, int)?> Prep(TShared shared)
    {
        var query = shared.TryGetValue("query", out var q) ? q as string ?? "" : "";
        var numResults = shared.TryGetValue("num_results", out var n) ? (int)n : 5;
        return Task.FromResult<(string, int)?>((query, numResults));
    }

    public override Task<List<Dictionary<string, string>>> Exec((string Query, int NumResults) input)
    {
        var (query, numResults) = input;
        
        Console.WriteLine($"[SearchNode] Searching for: {query}");
        
        var results = MockSearch(query, numResults);
        
        Console.WriteLine($"[SearchNode] Found {results.Count} results");
        
        return Task.FromResult(results);
    }

    public override Task<string?> Post(TShared shared, (string, int)? prepRes, List<Dictionary<string, string>>? execRes)
    {
        shared["search_results"] = execRes ?? new List<Dictionary<string, string>>();
        return Task.FromResult<string?>("default");
    }

    private List<Dictionary<string, string>> MockSearch(string query, int numResults)
    {
        var mockResults = new List<Dictionary<string, string>>
        {
            new() 
            { 
                ["title"] = $"Example Result 1 for: {query}", 
                ["snippet"] = $"This is a relevant snippet about {query}. It contains important information that matches your search query.",
                ["link"] = $"https://example.com/result1?q={Uri.EscapeDataString(query)}"
            },
            new() 
            { 
                ["title"] = $"Example Result 2 for: {query}", 
                ["snippet"] = $"Another great result related to {query}. This page provides additional context and details.",
                ["link"] = $"https://example.com/result2?q={Uri.EscapeDataString(query)}"
            },
            new() 
            { 
                ["title"] = $"Example Result 3 for: {query}", 
                ["snippet"] = $"Yet another useful page about {query} with more information and resources.",
                ["link"] = $"https://example.com/result3?q={Uri.EscapeDataString(query)}"
            }
        };
        
        return mockResults.Take(numResults).ToList();
    }
}

class AnalyzeResultsNode : Node<TShared, (string Query, List<Dictionary<string, string>> Results), Dictionary<string, object>>
{
    public override Task<(string, List<Dictionary<string, string>>)?> Prep(TShared shared)
    {
        var query = shared.TryGetValue("query", out var q) ? q as string ?? "" : "";
        var results = shared.TryGetValue("search_results", out var r) ? r as List<Dictionary<string, string>> ?? new() : new();
        return Task.FromResult<(string, List<Dictionary<string, string>>)?>((query, results));
    }

    public override Task<Dictionary<string, object>> Exec((string Query, List<Dictionary<string, string>> Results) input)
    {
        var (query, results) = input;
        
        Console.WriteLine($"[AnalyzeResultsNode] Analyzing {results.Count} results...");
        
        if (results.Count == 0)
        {
            return Task.FromResult<Dictionary<string, object>>(new Dictionary<string, object>
            {
                ["summary"] = "No search results to analyze",
                ["key_points"] = new List<string>(),
                ["follow_up_queries"] = new List<string>()
            });
        }
        
        var analysis = MockAnalyzeResults(query, results);
        
        Console.WriteLine("[AnalyzeResultsNode] Analysis complete");
        
        return Task.FromResult(analysis);
    }

    public override Task<string?> Post(TShared shared, (string, List<Dictionary<string, string>>)? prepRes, Dictionary<string, object>? execRes)
    {
        shared["analysis"] = execRes ?? new Dictionary<string, object>();
        
        Console.WriteLine("\nSearch Analysis:");
        Console.WriteLine($"\nSummary: {execRes?["summary"]}");
        
        Console.WriteLine("\nKey Points:");
        if (execRes?["key_points"] is List<string> keyPoints)
        {
            foreach (var point in keyPoints)
            {
                Console.WriteLine($"- {point}");
            }
        }
        
        Console.WriteLine("\nSuggested Follow-up Queries:");
        if (execRes?["follow_up_queries"] is List<string> followUps)
        {
            foreach (var fq in followUps)
            {
                Console.WriteLine($"- {fq}");
            }
        }
        
        return Task.FromResult<string?>("default");
    }

    private Dictionary<string, object> MockAnalyzeResults(string query, List<Dictionary<string, string>> results)
    {
        var formattedResults = new StringBuilder();
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            formattedResults.AppendLine($"Result {i + 1}:");
            formattedResults.AppendLine($"Title: {r["title"]}");
            formattedResults.AppendLine($"Snippet: {r["snippet"]}");
            formattedResults.AppendLine($"URL: {r["link"]}");
            formattedResults.AppendLine();
        }

        var summary = $"Based on the search results for '{query}', the most relevant findings indicate several important aspects of this topic. The results provide a good overview of the key concepts and practical applications.";
        
        var keyPoints = new List<string>
        {
            $"First key finding related to {query}",
            $"Second important point from the search results",
            $"Third notable aspect discovered during analysis",
            $"Additional relevant information about {query}",
            $"Further insights into the topic at hand"
        };
        
        var followUpQueries = new List<string>
        {
            $"more details about {query}",
            $"{query} best practices",
            $"recent developments in {query}"
        };
        
        return new Dictionary<string, object>
        {
            ["summary"] = summary,
            ["key_points"] = keyPoints,
            ["follow_up_queries"] = followUpQueries
        };
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        string query;
        if (args.Length > 0)
        {
            query = string.Join(" ", args);
        }
        else
        {
            Console.Write("Enter search query: ");
            query = Console.ReadLine() ?? "";
        }
        
        if (string.IsNullOrWhiteSpace(query))
        {
            Console.WriteLine("Error: Query is required");
            return;
        }
        
        var shared = new Dictionary<string, object>
        {
            ["query"] = query,
            ["num_results"] = 5
        };
        
        var searchNode = new SearchNode();
        var analyzeNode = new AnalyzeResultsNode();
        
        searchNode >> analyzeNode;
        
        var flow = new Flow<TShared>(searchNode);
        await flow.RunAsync(shared);
    }
}
