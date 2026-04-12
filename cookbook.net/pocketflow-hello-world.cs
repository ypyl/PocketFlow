#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class AnswerNode : Node<TShared, string, string>
{
    public override Task<string?> Prep(TShared shared)
        => Task.FromResult<string?>(shared["question"] as string);

    public override Task<string?> Exec(string? question)
    {
        Console.WriteLine($"[AnswerNode] Question: {question}");
        var answer = MockCallLlm(question ?? "");
        return Task.FromResult<string?>(answer);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        shared["answer"] = execRes ?? "";
        return Task.FromResult<string?>(null);
    }

    private string MockCallLlm(string question)
    {
        if (question.Contains("universe", StringComparison.OrdinalIgnoreCase))
            return "The end of the universe is likely when heat death occurs in approximately 10^100 years.";
        if (question.Contains("meaning", StringComparison.OrdinalIgnoreCase) && question.Contains("life", StringComparison.OrdinalIgnoreCase))
            return "The meaning of life is a deeply personal question that each individual must answer for themselves.";
        return "This is a mock answer from the LLM.";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var shared = new Dictionary<string, object>
        {
            ["question"] = "In one sentence, what's the end of universe?",
            ["answer"] = (string?)null
        };

        var flow = new Flow<TShared>(new AnswerNode());
        await flow.Run(shared);

        Console.WriteLine("\nQuestion: " + shared["question"]);
        Console.WriteLine("Answer: " + shared["answer"]);
    }
}
