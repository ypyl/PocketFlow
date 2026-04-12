#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class TextInput : Node<TShared, string?, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        if (!shared.ContainsKey("text"))
        {
            Console.Write("\nEnter text to convert: ");
            var text = Console.ReadLine() ?? "";
            shared["text"] = text;
        }
        return Task.FromResult<string?>(shared["text"] as string);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        Console.WriteLine("\nChoose transformation:");
        Console.WriteLine("1. Convert to UPPERCASE");
        Console.WriteLine("2. Convert to lowercase");
        Console.WriteLine("3. Reverse text");
        Console.WriteLine("4. Remove extra spaces");
        Console.WriteLine("5. Exit");
        
        Console.Write("\nYour choice (1-5): ");
        var choice = Console.ReadLine() ?? "";
        
        if (choice == "5")
            return Task.FromResult<string?>("exit");
        
        shared["choice"] = choice;
        return Task.FromResult<string?>("transform");
    }
}

class TextTransform : Node<TShared, (string Text, string Choice)?, string>
{
    public override Task<(string Text, string Choice)?> Prep(TShared shared)
    {
        var text = shared["text"] as string ?? "";
        var choice = shared["choice"] as string ?? "";
        return Task.FromResult<(string, string)?>((text, choice));
    }

    public override Task<string?> Exec((string Text, string Choice)? inputs)
    {
        var (text, choice) = inputs!.Value;
        
        var result = choice switch
        {
            "1" => text.ToUpper(),
            "2" => text.ToLower(),
            "3" => new string(text.Reverse().ToArray()),
            "4" => string.Join(" ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
            _ => "Invalid option!"
        };
        
        return Task.FromResult<string?>(result);
    }

    public override Task<string?> Post(TShared shared, (string Text, string Choice)? prepRes, string? execRes)
    {
        Console.WriteLine("\nResult: " + execRes);
        
        Console.Write("\nConvert another text? (y/n): ");
        var again = (Console.ReadLine() ?? "").ToLower();
        
        if (again == "y")
        {
            shared.Remove("text");
            return Task.FromResult<string?>("input");
        }
        return Task.FromResult<string?>("exit");
    }
}

class EndNode : Node<TShared, string?, string>
{
    public override Task<string?> Prep(TShared shared)
        => Task.FromResult<string?>(null);
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("\nWelcome to Text Converter!");
        Console.WriteLine("=========================");
        
        var textInput = new TextInput();
        var textTransform = new TextTransform();
        var endNode = new EndNode();
        
        textInput.On("transform").To(textTransform);
        textTransform.On("input").To(textInput);
        textTransform.On("exit").To(endNode);
        
        var flow = new Flow<TShared>(textInput);
        await flow.Run(new Dictionary<string, object>());
        
        Console.WriteLine("\nThank you for using Text Converter!");
    }
}
