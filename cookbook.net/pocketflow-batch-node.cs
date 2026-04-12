#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj
#:package CsvHelper@33.0.1

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class CSVProcessor : BatchNode<TShared, Dictionary<string, object>, Dictionary<string, object>>
{
    public int ChunkSize { get; }
    private readonly string _inputFile;

    public CSVProcessor(string inputFile, int chunkSize = 1000)
        : base(null, 1, 0, false)
    {
        _inputFile = inputFile;
        ChunkSize = chunkSize;
    }

    public override async Task<IEnumerable<Dictionary<string, object>>?> Prep(TShared shared)
    {
        var allRecords = new List<Dictionary<string, object>>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using var reader = new StreamReader(_inputFile);
        using var csv = new CsvReader(reader, config);
        
        await csv.ReadAsync();
        csv.ReadHeader();
        
        while (await csv.ReadAsync())
        {
            var record = new Dictionary<string, object>();
            for (var i = 0; i < csv.HeaderRecord?.Length; i++)
            {
                record[csv.HeaderRecord[i]] = csv.GetField(i) ?? "";
            }
            allRecords.Add(record);
        }

        return allRecords;
    }

    public override Task<Dictionary<string, object>> ExecItem(Dictionary<string, object> record)
    {
        var amount = Convert.ToDouble(record["amount"]);
        return Task.FromResult(new Dictionary<string, object>
        {
            ["total_sales"] = amount,
            ["num_transactions"] = 1,
            ["total_amount"] = amount
        });
    }

    public override Task<string?> Post(TShared shared, IEnumerable<Dictionary<string, object>>? prepRes, IList<Dictionary<string, object>>? execRes)
    {
        var results = execRes ?? new List<Dictionary<string, object>>();
        var totalSales = results.Sum(r => Convert.ToDouble(r["total_sales"]));
        var totalTransactions = results.Sum(r => Convert.ToInt32(r["num_transactions"]));
        var totalAmount = results.Sum(r => Convert.ToDouble(r["total_amount"]));

        shared["statistics"] = new Dictionary<string, object>
        {
            ["total_sales"] = totalSales,
            ["average_sale"] = totalAmount / totalTransactions,
            ["total_transactions"] = totalTransactions
        };

        return Task.FromResult<string?>("show_stats");
    }
}

class ShowStats : Node<TShared, Dictionary<string, object>, object?>
{
    public override Task<Dictionary<string, object>?> Prep(TShared shared)
    {
        var stats = shared["statistics"] as Dictionary<string, object>;
        return Task.FromResult(stats);
    }

    public override Task<object?> Exec(Dictionary<string, object>? prepRes) 
        => Task.FromResult<object?>(null);

    public override Task<string?> Post(TShared shared, Dictionary<string, object>? prepRes, object? execRes)
    {
        Console.WriteLine("\nFinal Statistics:");
        Console.WriteLine($"- Total Sales: ${Convert.ToDouble(prepRes!["total_sales"]):,0.00}");
        Console.WriteLine($"- Average Sale: ${Convert.ToDouble(prepRes["average_sale"]):,0.00}");
        var txCount = Convert.ToInt32(prepRes["total_transactions"]);
        Console.WriteLine($"- Total Transactions: {txCount:N0}");
        Console.WriteLine();
        return Task.FromResult<string?>(null);
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
        var inputFile = Path.Combine(dataDir, "sales.csv");
        
        Directory.CreateDirectory(dataDir);
        
        if (!File.Exists(inputFile))
        {
            Console.WriteLine("Creating sample sales.csv...");
            await GenerateSampleCSV(inputFile);
        }
        
        Console.WriteLine($"Processing sales.csv in chunks...");
        
        var shared = new Dictionary<string, object>
        {
            ["input_file"] = inputFile
        };
        
        var processor = new CSVProcessor(inputFile, 1000);
        var showStats = new ShowStats();
        
        processor.On("show_stats").To(showStats);
        
        var flow = new Flow<TShared>(processor);
        await flow.Run(shared);
    }

    static async Task GenerateSampleCSV(string filePath)
    {
        var random = new Random(42);
        var lines = new List<string> { "date,amount,product" };
        
        var startDate = new DateTime(2024, 1, 1);
        for (var i = 0; i < 10000; i++)
        {
            var date = startDate.AddDays(i);
            var amount = Math.Round(100 + random.NextDouble() * 60 - 30, 2);
            var product = (char)('A' + random.Next(3));
            lines.Add($"{date:yyyy-MM-dd},{amount},{product}");
        }
        
        await File.WriteAllLinesAsync(filePath, lines);
    }
}
