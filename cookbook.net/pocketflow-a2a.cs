#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Text.RegularExpressions;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class DecideAction : Node<TShared, (string Question, string Context)?, Dictionary<string, object>>
{
    public override Task<(string Question, string Context)?> Prep(TShared shared)
    {
        var context = shared.TryGetValue("context", out var ctx) ? ctx as string ?? "" : "No previous search";
        var question = shared["question"] as string ?? "";
        var tuple = (question, context);
        return Task.FromResult<(string Question, string Context)?>(tuple);
    }

    public override Task<Dictionary<string, object>?> Exec((string Question, string Context)? prepRes)
    {
        var (question, context) = prepRes!.Value;
        Console.WriteLine("🤔 Agent deciding what to do next...");

        var prompt = $"""
            ### CONTEXT
            You are a research assistant that can search the web.
            Question: {question}
            Previous Research: {context}

            ### ACTION SPACE
            [1] search - Look up more information on the web
            [2] answer - Answer the question with current knowledge

            Return your response in this format:
            action: search OR answer
            reason: <why you chose this action>
            answer: <if action is answer>
            search_query: <specific search query if action is search>
            """;

        Console.WriteLine($"[DecideAction] Calling LLM...");
        var response = MockCallLLM(prompt);

        var decision = ParseLLMResponse(response);
        return Task.FromResult<Dictionary<string, object>?>(decision);
    }

    public override Task<string?> Post(TShared shared, (string Question, string Context)? prepRes, Dictionary<string, object>? execRes)
    {
        if (execRes?["action"] as string == "search")
        {
            shared["search_query"] = execRes?["search_query"] as string ?? "";
            Console.WriteLine($"🔍 Agent decided to search for: {shared["search_query"]}");
        }
        else
        {
            shared["context"] = execRes["answer"] as string ?? "";
            Console.WriteLine("💡 Agent decided to answer the question");
        }

        return Task.FromResult<string?>(execRes["action"] as string);
    }

    private static string MockCallLLM(string prompt)
    {
        if (prompt.Contains("Previous Research: No previous search"))
        {
            return "action: search\nreason: Need more information\nsearch_query: 2024 Nobel Prize in Physics winner";
        }
        else
        {
            return "action: answer\nreason: We have enough information\nanswer: The 2024 Nobel Prize in Physics was awarded to John J. Hopfield and Geoffrey Hinton for their foundational work in neural networks.";
        }
    }

    private static Dictionary<string, object> ParseLLMResponse(string response)
    {
        var result = new Dictionary<string, object>();
        var lines = response.Split('\n');

        foreach (var line in lines)
        {
            if (line.StartsWith("action:"))
                result["action"] = line.Substring(7).Trim();
            else if (line.StartsWith("reason:"))
                result["reason"] = line.Substring(7).Trim();
            else if (line.StartsWith("answer:"))
                result["answer"] = line.Substring(7).Trim();
            else if (line.StartsWith("search_query:"))
                result["search_query"] = line.Substring(13).Trim();
        }

        if (!result.ContainsKey("action"))
            result["action"] = "answer";

        return result;
    }
}

class SearchWeb : Node<TShared, string, string>
{
    public override Task<string?> Prep(TShared shared)
        => Task.FromResult<string?>(shared["search_query"] as string ?? "");

    public override Task<string?> Exec(string? prepRes)
    {
        Console.WriteLine($"🌐 Searching the web for: {prepRes}");
        return Task.FromResult<string?>($"[Mock search results for: {prepRes}]");
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        var previous = shared.TryGetValue("context", out var prev) ? prev as string ?? "" : "";
        shared["context"] = previous + "\n\nSEARCH: " + prepRes + "\nRESULTS: " + execRes;
        Console.WriteLine("📚 Found information, analyzing results...");
        return Task.FromResult<string?>("decide");
    }
}

class AnswerQuestion : Node<TShared, (string Question, string Context)?, string>
{
    public override Task<(string Question, string Context)?> Prep(TShared shared)
    {
        var context = shared.TryGetValue("context", out var ctx) ? ctx as string ?? "" : "";
        return Task.FromResult<(string, string)?>((shared["question"] as string ?? "", context));
    }

    public override Task<string?> Exec((string Question, string Context)? prepRes)
    {
        var (question, context) = prepRes!.Value;
        Console.WriteLine("✍️ Crafting final answer...");

        var prompt = $"Question: {question}\nResearch: {context}";
        Console.WriteLine($"[AnswerQuestion] Calling LLM with prompt: {prompt}");

        return Task.FromResult<string?>("[Mock answer from LLM based on research]");
    }

    public override Task<string?> Post(TShared shared, (string Question, string Context)? prepRes, string? execRes)
    {
        shared["answer"] = execRes;
        Console.WriteLine("✅ Answer generated successfully");
        return Task.FromResult<string?>("done");
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var question = args.Length > 0 ? args[0] : "Who won the Nobel Prize in Physics 2024?";
        var shared = new Dictionary<string, object>
        {
            ["question"] = question
        };

        Console.WriteLine($"🤔 Processing question: {question}\n");

        var decide = new DecideAction();
        var search = new SearchWeb();
        var answer = new AnswerQuestion();

        decide.On("search").To(search);
        decide.On("answer").To(answer);
        search.On("decide").To(decide);

        var flow = new Flow<TShared>(decide);
        await flow.Run(shared);

        Console.WriteLine("\n🎯 Final Answer:");
        Console.WriteLine(shared.GetValueOrDefault("answer") ?? "No answer found");
    }
}
