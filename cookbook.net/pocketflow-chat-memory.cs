#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class GetUserQuestionNode : Node<TShared, string?, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        if (!shared.ContainsKey("messages"))
        {
            shared["messages"] = new List<Dictionary<string, string>>();
            Console.WriteLine("Welcome to the interactive chat! Type 'exit' to end the conversation.");
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
        if (execRes == null)
        {
            Console.WriteLine("\nGoodbye!");
            return Task.FromResult<string?>(null);
        }
        
        var messages = (List<Dictionary<string, string>>)shared["messages"];
        messages.Add(new Dictionary<string, string> { ["role"] = "user", ["content"] = execRes });
        
        return Task.FromResult<string?>("retrieve");
    }
}

class RetrieveNode : Node<TShared, Dictionary<string, object>?, Dictionary<string, object>>
{
    public override Task<Dictionary<string, object>?> Prep(TShared shared)
    {
        var messages = (List<Dictionary<string, string>>)shared["messages"];
        if (messages.Count == 0)
        {
            return Task.FromResult<Dictionary<string, object>?>(null);
        }
        
        string latestUserMsg = "";
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].TryGetValue("role", out var role) && role?.ToString() == "user")
            {
                latestUserMsg = messages[i].TryGetValue("content", out var content) ? content?.ToString() ?? "" : "";
                break;
            }
        }
        
        if (!shared.ContainsKey("vector_items") || ((List<object>)shared["vector_items"]).Count == 0)
        {
            return Task.FromResult<Dictionary<string, object>?>(null);
        }
        
        return Task.FromResult<Dictionary<string, object>?>(new Dictionary<string, object>
        {
            ["query"] = latestUserMsg,
            ["vector_items"] = shared["vector_items"]
        });
    }

    public override Task<Dictionary<string, object>?> Exec(Dictionary<string, object>? inputs)
    {
        if (inputs == null)
        {
            return Task.FromResult<Dictionary<string, object>?>(null);
        }
        
        var query = inputs["query"]?.ToString() ?? "";
        var vectorItems = (List<object>)inputs["vector_items"];
        
        Console.WriteLine($"[RetrieveNode] Finding relevant conversation for: {query.Substring(0, Math.Min(30, query.Length))}...");
        
        var result = MockSearchVectors(query, vectorItems);
        return Task.FromResult<Dictionary<string, object>?>(result);
    }

    public override Task<string?> Post(TShared shared, Dictionary<string, object>? prepRes, Dictionary<string, object>? execRes)
    {
        if (execRes != null)
        {
            shared["retrieved_conversation"] = execRes["conversation"];
            Console.WriteLine($"[RetrieveNode] Retrieved conversation (distance: {execRes["distance"]})");
        }
        else
        {
            shared["retrieved_conversation"] = null;
        }
        
        return Task.FromResult<string?>("answer");
    }

    private Dictionary<string, object>? MockSearchVectors(string query, List<object> vectorItems)
    {
        if (vectorItems.Count == 0)
            return null;
        
        return new Dictionary<string, object>
        {
            ["conversation"] = vectorItems[0],
            ["distance"] = 0.5
        };
    }
}

class AnswerNode : Node<TShared, List<Dictionary<string, string>>?, string>
{
    public override Task<List<Dictionary<string, string>>?> Prep(TShared shared)
    {
        var messages = (List<Dictionary<string, string>>)shared["messages"];
        if (messages.Count == 0)
            return Task.FromResult<List<Dictionary<string, string>>?>(null);
        
        var recentMessages = messages.Count > 6 ? messages.Skip(messages.Count - 6).ToList() : messages;
        
        var context = new List<Dictionary<string, string>>();
        
        if (shared.TryGetValue("retrieved_conversation", out var retrieved) && retrieved is List<Dictionary<string, string>> retrievedConv)
        {
            context.Add(new Dictionary<string, string> { ["role"] = "system", ["content"] = "The following is a relevant past conversation that may help with the current query:" });
            context.AddRange(retrievedConv);
            context.Add(new Dictionary<string, string> { ["role"] = "system", ["content"] = "Now continue the current conversation:" });
        }
        
        context.AddRange(recentMessages);
        
        return Task.FromResult<List<Dictionary<string, string>>?>(context);
    }

    public override Task<string?> Exec(List<Dictionary<string, string>>? messages)
    {
        if (messages == null)
            return Task.FromResult<string?>(null);
        
        Console.WriteLine("[AnswerNode] Generating response...");
        return Task.FromResult<string?>(MockCallLLM(messages));
    }

    public override Task<string?> Post(TShared shared, List<Dictionary<string, string>>? prepRes, string? execRes)
    {
        if (prepRes == null || execRes == null)
            return Task.FromResult<string?>(null);
        
        Console.WriteLine($"\nAssistant: {execRes}");
        
        var messages = (List<Dictionary<string, string>>)shared["messages"];
        messages.Add(new Dictionary<string, string> { ["role"] = "assistant", ["content"] = execRes });
        
        if (messages.Count > 6)
            return Task.FromResult<string?>("embed");
        
        return Task.FromResult<string?>("question");
    }

    private string MockCallLLM(List<Dictionary<string, string>> messages)
    {
        var lastUserMsg = "";
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].TryGetValue("role", out var role) && role?.ToString() == "user")
            {
                lastUserMsg = messages[i].TryGetValue("content", out var content) ? content?.ToString() ?? "" : "";
                break;
            }
        }
        
        if (lastUserMsg.ToLower().Contains("hello"))
            return "Hello! How can I help you today?";
        if (lastUserMsg.ToLower().Contains("help"))
            return "I'd be happy to help! This is a mock chat with memory. Ask me anything.";
        
        return $"You said: '{lastUserMsg}'. This is a mock response with memory context ({messages.Count} messages).";
    }
}

class EmbedNode : Node<TShared, List<Dictionary<string, string>>?, Dictionary<string, object>>
{
    public override Task<List<Dictionary<string, string>>?> Prep(TShared shared)
    {
        var messages = (List<Dictionary<string, string>>)shared["messages"];
        if (messages.Count <= 6)
            return Task.FromResult<List<Dictionary<string, string>>?>(null);
        
        var oldestPair = messages.Take(2).ToList();
        shared["messages"] = messages.Skip(2).ToList();
        
        return Task.FromResult<List<Dictionary<string, string>>?>(oldestPair);
    }

    public override Task<Dictionary<string, object>?> Exec(List<Dictionary<string, string>>? conversation)
    {
        if (conversation == null || conversation.Count == 0)
            return Task.FromResult<Dictionary<string, object>?>(null);
        
        var userMsg = conversation.FirstOrDefault(m => m.TryGetValue("role", out var r) && r?.ToString() == "user");
        var assistantMsg = conversation.FirstOrDefault(m => m.TryGetValue("role", out var r) && r?.ToString() == "assistant");
        
        var combined = $"User: {userMsg?["content"]} Assistant: {assistantMsg?["content"]}";
        Console.WriteLine($"[EmbedNode] Embedding conversation: {combined.Substring(0, Math.Min(50, combined.Length))}...");
        
        var embedding = MockGetEmbedding(combined);
        
        return Task.FromResult<Dictionary<string, object>?>(new Dictionary<string, object>
        {
            ["conversation"] = conversation,
            ["embedding"] = embedding
        });
    }

    public override Task<string?> Post(TShared shared, List<Dictionary<string, string>>? prepRes, Dictionary<string, object>? execRes)
    {
        if (execRes == null)
            return Task.FromResult<string?>("question");
        
        if (!shared.ContainsKey("vector_items"))
        {
            shared["vector_items"] = new List<object>();
        }
        
        var position = ((List<object>)shared["vector_items"]).Count;
        ((List<object>)shared["vector_items"]).Add(execRes["conversation"]);
        
        Console.WriteLine($"[EmbedNode] Added conversation to index at position {position}");
        Console.WriteLine($"[EmbedNode] Index now contains {((List<object>)shared["vector_items"]).Count} conversations");
        
        return Task.FromResult<string?>("question");
    }

    private double[] MockGetEmbedding(string text)
    {
        return new double[] { 0.1, 0.2, 0.3 };
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=".PadRight(50, '='));
        Console.WriteLine("PocketFlow Chat with Memory");
        Console.WriteLine("=".PadRight(50, '='));
        Console.WriteLine("This chat keeps your 3 most recent conversations");
        Console.WriteLine("and brings back relevant past conversations when helpful");
        Console.WriteLine("Type 'exit' to end the conversation");
        Console.WriteLine("=".PadRight(50, '='));
        
        var questionNode = new GetUserQuestionNode();
        var retrieveNode = new RetrieveNode();
        var answerNode = new AnswerNode();
        var embedNode = new EmbedNode();
        
        questionNode.On("retrieve").To(retrieveNode);
        retrieveNode.On("answer").To(answerNode);
        answerNode.On("embed").To(embedNode);
        answerNode.On("question").To(questionNode);
        embedNode.On("question").To(questionNode);
        
        var flow = new Flow<TShared>(questionNode);
        await flow.Run(new Dictionary<string, object>());
    }
}
