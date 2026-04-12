#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class ChatNode : Node<TShared, List<Dictionary<string, string>>?, string>
{
    public override Task<List<Dictionary<string, string>>?> Prep(TShared shared)
    {
        if (!shared.ContainsKey("messages"))
        {
            shared["messages"] = new List<Dictionary<string, string>>();
            Console.WriteLine("Welcome to the chat! Type 'exit' to end the conversation.");
        }
        
        Console.Write("\nYou: ");
        var userInput = Console.ReadLine() ?? "";
        
        if (userInput.ToLower() == "exit")
        {
            return Task.FromResult<List<Dictionary<string, string>>?>(null);
        }
        
        var messages = (List<Dictionary<string, string>>)shared["messages"];
        messages.Add(new Dictionary<string, string> { ["role"] = "user", ["content"] = userInput });
        
        return Task.FromResult<List<Dictionary<string, string>>?>(messages);
    }

    public override Task<string?> Exec(List<Dictionary<string, string>>? messages)
    {
        if (messages == null)
        {
            return Task.FromResult<string?>(null);
        }
        
        Console.WriteLine("[ChatNode] Calling LLM...");
        var response = MockCallLLM(messages);
        return Task.FromResult<string?>(response);
    }

    public override Task<string?> Post(TShared shared, List<Dictionary<string, string>>? prepRes, string? execRes)
    {
        if (prepRes == null || execRes == null)
        {
            Console.WriteLine("\nGoodbye!");
            return Task.FromResult<string?>(null);
        }
        
        Console.WriteLine($"\nAssistant: {execRes}");
        
        var messages = (List<Dictionary<string, string>>)shared["messages"];
        messages.Add(new Dictionary<string, string> { ["role"] = "assistant", ["content"] = execRes });
        
        return Task.FromResult<string?>("continue");
    }

    private static string MockCallLLM(List<Dictionary<string, string>> messages)
    {
        var lastUserMessage = messages.LastOrDefault(m => m["role"] == "user")?["content"] ?? "";
        
        if (lastUserMessage.ToLower().Contains("hello"))
            return "Hello! How can I help you today?";
        if (lastUserMessage.ToLower().Contains("meaning of life"))
            return "The meaning of life is a deeply personal question. Some find meaning through relationships, work, or spiritual beliefs.";
        if (lastUserMessage.ToLower().Contains("weather"))
            return "I don't have access to weather information, but I hope it's nice where you are!";
        
        return $"I understand you said: '{lastUserMessage}'. This is a mock response for demonstration purposes.";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var chatNode = new ChatNode();
        chatNode.On("continue").To(chatNode);
        
        var flow = new Flow<TShared>(chatNode);
        await flow.Run(new Dictionary<string, object>());
    }
}
