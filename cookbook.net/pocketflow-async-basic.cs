#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class FetchRecipes : Node<TShared, string?, List<string>>
{
    public override async Task<string?> Prep(TShared shared)
    {
        Console.Write("Enter ingredient: ");
        var ingredient = await Task.Run(() => Console.ReadLine() ?? "");
        return ingredient;
    }

    public override async Task<List<string>?> Exec(string? ingredient)
    {
        Console.WriteLine($"Fetching recipes for {ingredient}...");
        await Task.Delay(1000);
        
        var recipes = new List<string>
        {
            $"{ingredient} Stir Fry",
            $"Grilled {ingredient} with Herbs",
            $"Baked {ingredient} with Vegetables"
        };
        
        Console.WriteLine($"Found {recipes.Count} recipes.");
        return recipes;
    }

    public override Task<string?> Post(TShared shared, string? prepRes, List<string>? execRes)
    {
        shared["recipes"] = execRes ?? new List<string>();
        shared["ingredient"] = prepRes ?? "";
        return Task.FromResult<string?>("suggest");
    }
}

class SuggestRecipe : Node<TShared, List<string>, string>
{
    public override Task<List<string>?> Prep(TShared shared)
    {
        var recipes = shared.TryGetValue("recipes", out var r) ? r as List<string> ?? new List<string>() : new List<string>();
        return Task.FromResult<List<string>?>(recipes);
    }

    public override async Task<string?> Exec(List<string>? recipes)
    {
        Console.WriteLine("\nSuggesting best recipe...");
        await Task.Delay(1000);
        
        var suggestion = recipes?.Count > 1 ? recipes[1] : recipes?.FirstOrDefault() ?? "";
        Console.WriteLine($"How about: {suggestion}");
        return suggestion;
    }

    public override Task<string?> Post(TShared shared, List<string>? prepRes, string? execRes)
    {
        shared["suggestion"] = execRes ?? "";
        return Task.FromResult<string?>("approve");
    }
}

class GetApproval : Node<TShared, string, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        var suggestion = shared.TryGetValue("suggestion", out var s) ? s as string ?? "" : "";
        return Task.FromResult<string?>(suggestion);
    }

    public override Task<string?> Exec(string? suggestion)
    {
        Console.Write($"\nAccept this recipe? (y/n): ");
        var answer = (Console.ReadLine() ?? "").ToLower();
        return Task.FromResult<string?>(answer);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? answer)
    {
        if (answer == "y")
        {
            Console.WriteLine("\nGreat choice! Here's your recipe...");
            Console.WriteLine($"Recipe: {shared["suggestion"]}");
            Console.WriteLine($"Ingredient: {shared["ingredient"]}");
            return Task.FromResult<string?>("accept");
        }
        else
        {
            Console.WriteLine("\nLet's try another recipe...");
            return Task.FromResult<string?>("retry");
        }
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("\nWelcome to Recipe Finder!");
        Console.WriteLine("------------------------");

        var shared = new Dictionary<string, object>();

        var fetch = new FetchRecipes();
        var suggest = new SuggestRecipe();
        var approve = new GetApproval();

        fetch.On("suggest").To(suggest);
        suggest.On("approve").To(approve);
        approve.On("retry").To(suggest);
        approve.On("accept").To(null!);

        var flow = new Flow<TShared>(fetch);
        await flow.Run(shared);

        Console.WriteLine("\nThanks for using Recipe Finder!");
    }
}
