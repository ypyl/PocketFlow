#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Text.RegularExpressions;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class UserInputNode : Node<TShared, string?, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        if (!shared.ContainsKey("messages"))
        {
            shared["messages"] = new List<Dictionary<string, string>>();
            Console.WriteLine("Welcome to the Travel Advisor Chat! Type 'exit' to end the conversation.");
        }
        return Task.FromResult<string?>(null);
    }

    public override Task<string?> Exec(string? prepRes)
    {
        Console.Write("\nYou: ");
        var userInput = Console.ReadLine() ?? "";
        return Task.FromResult<string?>(userInput);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        if (execRes != null && execRes.ToLower() == "exit")
        {
            Console.WriteLine("\nGoodbye! Safe travels!");
            return Task.FromResult<string?>(null);
        }
        
        shared["user_input"] = execRes ?? "";
        return Task.FromResult<string?>("validate");
    }
}

class GuardrailNode : Node<TShared, string?, (bool IsValid, string Reason)>
{
    public override Task<string?> Prep(TShared shared)
    {
        var userInput = shared.TryGetValue("user_input", out var ui) ? ui as string ?? "" : "";
        return Task.FromResult<string?>(userInput);
    }

    public override Task<(bool IsValid, string Reason)> Exec(string? userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return Task.FromResult<(bool IsValid, string Reason)>((false, "Your query is empty. Please provide a travel-related question."));
        }
        
        if (userInput.Trim().Length < 3)
        {
            return Task.FromResult<(bool IsValid, string Reason)>((false, "Your query is too short. Please provide more details about your travel question."));
        }
        
        var prompt = $@"Evaluate if the following user query is related to travel advice, destinations, planning, or other travel topics.
The chat should ONLY answer travel-related questions and reject any off-topic, harmful, or inappropriate queries.
User query: {userInput}
Return your evaluation in YAML format:
valid: true/false
reason: [Explain why the query is valid or invalid]";

        Console.WriteLine("[GuardrailNode] Validating input...");
        var response = MockCallLLM(prompt);
        var result = ParseYamlResponse(response);
        
        return Task.FromResult<(bool IsValid, string Reason)>((result.IsValid, result.Reason));
    }

    public override Task<string?> Post(TShared shared, string? prepRes, (bool IsValid, string Reason) execRes)
    {
        if (!execRes.IsValid)
        {
            var message = execRes.Reason;
            Console.WriteLine($"\nTravel Advisor: {message}");
            return Task.FromResult<string?>("retry");
        }
        
        var messages = (List<Dictionary<string, string>>)shared["messages"];
        messages.Add(new Dictionary<string, string> { ["role"] = "user", ["content"] = shared["user_input"] as string ?? "" });
        return Task.FromResult<string?>("process");
    }

    private static string MockCallLLM(string prompt)
    {
        if (prompt.Contains("travel"))
        {
            return "valid: true\nreason: The query is about travel";
        }
        return "valid: false\nreason: The query is not related to travel";
    }

    private static (bool IsValid, string Reason) ParseYamlResponse(string response)
    {
        var valid = false;
        var reason = "";
        
        var lines = response.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("valid:"))
                valid = line.Contains("true");
            else if (line.StartsWith("reason:"))
                reason = line.Substring(8).Trim();
        }
        
        return (valid, reason);
    }
}

class LLMNode : Node<TShared, List<Dictionary<string, string>>, string>
{
    public override Task<List<Dictionary<string, string>>?> Prep(TShared shared)
    {
        var messages = (List<Dictionary<string, string>>)shared["messages"];
        
        if (!messages.Any(m => m.TryGetValue("role", out var role) && role?.ToString() == "system"))
        {
            messages.Insert(0, new Dictionary<string, string>
            {
                ["role"] = "system",
                ["content"] = "You are a helpful travel advisor. Keep responses concise in 100 words."
            });
        }
        
        return Task.FromResult<List<Dictionary<string, string>>?>(messages);
    }

    public override Task<string?> Exec(List<Dictionary<string, string>>? messages)
    {
        Console.WriteLine("[LLMNode] Processing request...");
        var response = MockCallLLM(messages ?? new List<Dictionary<string, string>>());
        return Task.FromResult<string?>(response);
    }

    public override Task<string?> Post(TShared shared, List<Dictionary<string, string>>? prepRes, string? execRes)
    {
        Console.WriteLine($"\nTravel Advisor: {execRes}");
        
        var messages = (List<Dictionary<string, string>>)shared["messages"];
        messages.Add(new Dictionary<string, string> { ["role"] = "assistant", ["content"] = execRes ?? "" });
        
        return Task.FromResult<string?>("continue");
    }

    private static string MockCallLLM(List<Dictionary<string, string>> messages)
    {
        string lastUserMessage = "";
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].TryGetValue("role", out var role) && role?.ToString() == "user")
            {
                lastUserMessage = messages[i].TryGetValue("content", out var content) ? content?.ToString() ?? "" : "";
                break;
            }
        }
        
        if (lastUserMessage.ToLower().Contains("paris"))
            return "Paris is a beautiful destination! The Eiffel Tower, Louvre Museum, and charming neighborhoods like Montmartre are must-sees.";
        if (lastUserMessage.ToLower().Contains("japan"))
            return "Japan offers amazing experiences from Tokyo's modern culture to Kyoto's ancient temples. Don't miss cherry blossom season!";
        if (lastUserMessage.ToLower().Contains("beach"))
            return "For a great beach vacation, consider Hawaii, the Maldives, or the Greek islands for crystal clear waters.";
        
        return "As a travel advisor, I'd be happy to help you plan your trip! What destination are you interested in?";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var userInput = new UserInputNode();
        var guardrail = new GuardrailNode();
        var llm = new LLMNode();
        
        userInput.On("validate").To(guardrail);
        guardrail.On("retry").To(userInput);
        guardrail.On("process").To(llm);
        llm.On("continue").To(userInput);
        
        var flow = new Flow<TShared>(userInput);
        await flow.Run(new Dictionary<string, object>());
    }
}
