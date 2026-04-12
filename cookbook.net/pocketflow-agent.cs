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
[1] search
  Description: Look up more information on the web
  Parameters:
    - query (str): What to search for

[2] answer
  Description: Answer the question with current knowledge
  Parameters:
    - answer (str): Final answer to the question

## NEXT ACTION
Decide the next action based on the context and available actions.
Return your response in this format:

```yaml
thinking: |
    <your step-by-step reasoning process>
action: search OR answer
reason: |
    <why you chose this action - always use block scalar>
answer: |
    <if action is answer - always use block scalar, leave empty if searching>
search_query: <specific search query if action is search (plain string)>
```
IMPORTANT: Make sure to:
1. ALWAYS use the | block scalar for thinking, reason and answer so colons or quotes inside the text do not break YAML.
2. Use proper indentation (4 spaces) for all multi-line fields under |.
3. Keep search_query as a single line string without the | character.
""";

        Console.WriteLine($"[DecideAction] Calling LLM...");
        var response = MockCallLLM(prompt);

        var decision = ParseLLMResponse(response);
        return Task.FromResult<Dictionary<string, object>?>(decision);
    }

    public override Task<string?> Post(TShared shared, (string Question, string Context)? prepRes, Dictionary<string, object>? execRes)
    {
        var action = execRes.TryGetValue("action", out var act) ? act as string ?? "answer" : "answer";
        
        if (action == "search")
        {
            shared["search_query"] = execRes.TryGetValue("search_query", out var sq) ? sq as string ?? "" : "";
            Console.WriteLine($"🔍 Agent decided to search for: {shared["search_query"]}");
        }
        else
        {
            shared["context"] = execRes.TryGetValue("answer", out var ans) ? ans as string ?? "" : "";
            Console.WriteLine("💡 Agent decided to answer the question");
        }

        return Task.FromResult<string?>(action);
    }

    private static string MockCallLLM(string prompt)
    {
        if (prompt.Contains("Previous Research: No previous search"))
        {
            return """
action: search
reason: Need more information to answer the question accurately
search_query: 2024 Nobel Prize in Physics winner
""";
        }
        else
        {
            return """
action: answer
reason: We have gathered sufficient information through web search
answer: The 2024 Nobel Prize in Physics was awarded to John J. Hopfield and Geoffrey Hinton for their foundational work in neural networks and deep learning.
""";
        }
    }

    private static Dictionary<string, object> ParseLLMResponse(string response)
    {
        var result = new Dictionary<string, object>();
        var yamlMatch = Regex.Match(response, @"```yaml\s*(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        string yamlContent;
        if (yamlMatch.Success)
        {
            yamlContent = yamlMatch.Groups[1].Value;
        }
        else
        {
            yamlContent = response;
        }

        var lines = yamlContent.Split('\n');
        string? currentKey = null;
        var inBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            var keyMatch = Regex.Match(trimmed, @"^(action|thinking|reason|answer|search_query):\s*(\|)?");
            if (keyMatch.Success)
            {
                currentKey = keyMatch.Groups[1].Value;
                var hasBlockScalar = keyMatch.Groups[2].Success;
                
                if (hasBlockScalar)
                {
                    inBlock = true;
                    result[currentKey] = "";
                }
                else
                {
                    var value = trimmed.Substring(keyMatch.Length).Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        result[currentKey] = value;
                    }
                    inBlock = false;
                }
            }
            else if (currentKey != null && inBlock && !string.IsNullOrWhiteSpace(trimmed))
            {
                var existing = result.TryGetValue(currentKey, out var val) ? val as string : null;
                result[currentKey] = (existing ?? "") + (existing != null && existing.Length > 0 ? "\n" : "") + trimmed;
            }
            else if (currentKey != null && !inBlock && !trimmed.StartsWith("#") && !string.IsNullOrWhiteSpace(trimmed))
            {
                var value = trimmed;
                if (result.TryGetValue(currentKey, out var existing) && existing is string s && !string.IsNullOrEmpty(s))
                {
                    result[currentKey] = s + " " + value;
                }
                else
                {
                    result[currentKey] = value;
                }
            }
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
        return Task.FromResult<string?>(MockSearchWeb(prepRes ?? ""));
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        var previous = shared.TryGetValue("context", out var prev) ? prev as string ?? "" : "";
        shared["context"] = previous + "\n\nSEARCH: " + prepRes + "\nRESULTS: " + execRes;
        Console.WriteLine("📚 Found information, analyzing results...");
        return Task.FromResult<string?>("decide");
    }

    private static string MockSearchWeb(string query)
    {
        return $"[Mock search results for: {query}] - Nobel Prize in Physics 2024 was awarded to John Hopfield and Geoffrey Hinton for their foundational work in neural networks.";
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

        var prompt = $"""
### CONTEXT
Based on the following information, answer the question.
Question: {question}
Research: {context}

## YOUR ANSWER:
Provide a comprehensive answer using the research results.
""";
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
        var question = "Who won the Nobel Prize in Physics 2024?";
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--") && i + 1 < args.Length)
            {
                question = args[i + 1];
                break;
            }
        }

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
        object? answerVal;
        shared.TryGetValue("answer", out answerVal);
        Console.WriteLine(answerVal?.ToString() ?? "No answer found");
    }
}
