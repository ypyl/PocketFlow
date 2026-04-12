#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class GenerateTestCases : Node<TShared, string, Dictionary<string, object>>
{
    public override Task<string?> Prep(TShared shared)
        => Task.FromResult<string?>(shared["problem"] as string);

    public override Task<Dictionary<string, object>?> Exec(string? problem)
    {
        Console.WriteLine("[GenerateTestCases] Generating test cases...");
        var result = MockGenerateTestCases(problem ?? "");
        return Task.FromResult<Dictionary<string, object>?>(result);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, Dictionary<string, object>? execRes)
    {
        shared["test_cases"] = execRes?["test_cases"] ?? new List<Dictionary<string, object>>();
        
        var testCases = (List<Dictionary<string, object>>)shared["test_cases"];
        Console.WriteLine($"\n=== Generated {testCases.Count} Test Cases ===");
        for (int i = 0; i < testCases.Count; i++)
        {
            var tc = testCases[i];
            Console.WriteLine($"{i + 1}. {tc["name"]}");
            Console.WriteLine($"   input: {tc["input"]}");
            Console.WriteLine($"   expected: {tc["expected"]}");
        }
        
        return Task.FromResult<string?>("default");
    }

    private Dictionary<string, object> MockGenerateTestCases(string problem)
    {
        return new Dictionary<string, object>
        {
            ["test_cases"] = new List<Dictionary<string, object>>
            {
                new() { ["name"] = "Basic case", ["input"] = "nums = [2,7,11,15], target = 9", ["expected"] = "[0,1]" },
                new() { ["name"] = "Edge case - same elements", ["input"] = "nums = [3,3], target = 6", ["expected"] = "[0,1]" },
                new() { ["name"] = "Edge case - middle elements", ["input"] = "nums = [3,2,4], target = 6", ["expected"] = "[1,2]" }
            }
        };
    }
}

class ImplementFunction : Node<TShared, (string Problem, List<Dictionary<string, object>> TestCases), string>
{
    public override Task<(string Problem, List<Dictionary<string, object>> TestCases)> Prep(TShared shared)
    {
        var problem = shared["problem"] as string ?? "";
        var testCases = shared["test_cases"] as List<Dictionary<string, object>> ?? new List<Dictionary<string, object>>();
        return Task.FromResult<(string, List<Dictionary<string, object>>)>((problem, testCases));
    }

    public override Task<string?> Exec((string Problem, List<Dictionary<string, object>> TestCases) inputs)
    {
        Console.WriteLine("[ImplementFunction] Implementing function...");
        var functionCode = MockImplementFunction();
        return Task.FromResult<string?>(functionCode);
    }

    public override Task<string?> Post(TShared shared, (string Problem, List<Dictionary<string, object>> TestCases) prepRes, string? execRes)
    {
        shared["function_code"] = execRes ?? "";
        Console.WriteLine($"\n=== Implemented Function ===");
        Console.WriteLine(execRes);
        return Task.FromResult<string?>("default");
    }

    private string MockImplementFunction()
    {
        return @"def run_code(nums, target):
    for i in range(len(nums)):
        for j in range(i + 1, len(nums)):
            if nums[i] + nums[j] == target:
                return [i, j]
    return []";
    }
}

class RunTests : BatchNode<TShared, (string FunctionCode, Dictionary<string, object> TestCase), Dictionary<string, object>>
{
    public RunTests(IDictionary<string, object>? defaultParams = null, int maxRetries = 1, double wait = 0, bool enableParallel = false)
        : base(defaultParams, maxRetries, wait, enableParallel)
    {
    }

    public override Task<IEnumerable<(string FunctionCode, Dictionary<string, object> TestCase)>?> Prep(TShared shared)
    {
        var functionCode = shared["function_code"] as string ?? "";
        var testCases = shared["test_cases"] as List<Dictionary<string, object>> ?? new List<Dictionary<string, object>>();
        var items = testCases.Select(tc => (functionCode, tc)).ToList();
        return Task.FromResult<IEnumerable<(string, Dictionary<string, object>)>?>(items);
    }

    public override Task<Dictionary<string, object>> ExecItem((string FunctionCode, Dictionary<string, object> TestCase) item)
    {
        var (functionCode, testCase) = item;
        Console.WriteLine($"[RunTests] Running test: {testCase["name"]}");
        
        return Task.FromResult(new Dictionary<string, object>
        {
            ["test_case"] = testCase,
            ["passed"] = true,
            ["actual"] = "[0,1]",
            ["expected"] = testCase["expected"],
            ["error"] = null
        });
    }

    public override Task<string?> Post(TShared shared, IEnumerable<(string FunctionCode, Dictionary<string, object> TestCase)>? prepRes, IList<Dictionary<string, object>>? execResList)
    {
        shared["test_results"] = execResList ?? new List<Dictionary<string, object>>();
        
        var passedCount = execResList?.Count(r => r.TryGetValue("passed", out var p) && p is true) ?? 0;
        var totalCount = execResList?.Count ?? 0;
        
        Console.WriteLine($"\n=== Test Results: {passedCount}/{totalCount} Passed ===");
        
        var failedTests = execResList?.Where(r => !r.TryGetValue("passed", out var p) || !(p is true)).ToList() ?? new List<Dictionary<string, object>>();
        if (failedTests.Count > 0)
        {
            Console.WriteLine("Failed tests:");
            for (int i = 0; i < failedTests.Count; i++)
            {
                var result = failedTests[i];
                var testCase = result["test_case"] as Dictionary<string, object>;
                Console.WriteLine($"{i + 1}. {testCase?["name"]}:");
                if (result.TryGetValue("error", out var err) && err != null)
                    Console.WriteLine($"   error: {err}");
                else
                    Console.WriteLine($"   output: {result["actual"]}");
                Console.WriteLine($"   expected: {result["expected"]}");
            }
        }
        
        shared["iteration_count"] = (int)(shared.TryGetValue("iteration_count", out var ic) ? ic : 0) + 1;
        
        if (passedCount == totalCount)
            return Task.FromResult<string?>("success");
        else if ((int)(shared.TryGetValue("iteration_count", out var ic2) ? ic2 : 0) >= (int)(shared.TryGetValue("max_iterations", out var mi) ? mi : 5))
            return Task.FromResult<string?>("max_iterations");
        else
            return Task.FromResult<string?>("failure");
    }
}

class Revise : Node<TShared, Dictionary<string, object>, Dictionary<string, object>>
{
    public override Task<Dictionary<string, object>?> Prep(TShared shared)
    {
        var testResults = shared["test_results"] as IList<Dictionary<string, object>> ?? new List<Dictionary<string, object>>();
        var failedTests = testResults.Where(r => !r.TryGetValue("passed", out var p) || !(p is true)).ToList();
        
        return Task.FromResult<Dictionary<string, object>?>(new Dictionary<string, object>
        {
            ["problem"] = shared["problem"] ?? "",
            ["test_cases"] = shared["test_cases"] ?? new List<Dictionary<string, object>>(),
            ["function_code"] = shared["function_code"] ?? "",
            ["failed_tests"] = failedTests
        });
    }

    public override Task<Dictionary<string, object>?> Exec(Dictionary<string, object>? inputs)
    {
        Console.WriteLine("[Revise] Revising code/tests...");
        
        var revision = new Dictionary<string, object>
        {
            ["reasoning"] = "Mock revision - test passed on second try",
            ["function_code"] = @"def run_code(nums, target):
    hashmap = {}
    for i, num in enumerate(nums):
        complement = target - num
        if complement in hashmap:
            return [hashmap[complement], i]
        hashmap[num] = i
    return []"
        };
        
        return Task.FromResult<Dictionary<string, object>?>(revision);
    }

    public override Task<string?> Post(TShared shared, Dictionary<string, object>? prepRes, Dictionary<string, object>? execRes)
    {
        var iteration = shared.TryGetValue("iteration_count", out var ic) ? ic : 0;
        Console.WriteLine($"\n=== Revisions (Iteration {iteration}) ===");
        
        if (execRes?.ContainsKey("test_cases") == true)
        {
            Console.WriteLine("Revising test cases:");
            shared["test_cases"] = execRes["test_cases"];
        }
        
        if (execRes?.ContainsKey("function_code") == true)
        {
            Console.WriteLine("Revising function code:");
            Console.WriteLine(execRes["function_code"]);
            shared["function_code"] = execRes["function_code"];
        }
        
        return Task.FromResult<string?>("default");
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var problem = args.Length > 0 ? string.Join(" ", args) : @"Two Sum

Given an array of integers nums and an integer target, return indices of the two numbers such that they add up to target.

Example: nums = [2,7,11,15], target = 9, Output: [0,1]";

        Console.WriteLine("Starting PocketFlow Code Generator...");

        var shared = new Dictionary<string, object>
        {
            ["problem"] = problem,
            ["test_cases"] = new List<Dictionary<string, object>>(),
            ["function_code"] = "",
            ["test_results"] = new List<Dictionary<string, object>>(),
            ["iteration_count"] = 0,
            ["max_iterations"] = 5
        };

        var generateTests = new GenerateTestCases();
        var implementFunction = new ImplementFunction();
        var runTests = new RunTests();
        var revise = new Revise();

        generateTests.On("default").To(implementFunction);
        implementFunction.On("default").To(runTests);
        runTests.On("failure").To(revise);
        revise.On("default").To(runTests);

        var flow = new Flow<TShared>(generateTests);
        await flow.Run(shared);

        Console.WriteLine("\n=== Final Results ===");
        Console.WriteLine($"Problem: {(shared["problem"] as string)?.Substring(0, Math.Min(50, (shared["problem"] as string)?.Length ?? 0))}...");
        Console.WriteLine($"Iterations: {shared["iteration_count"]}");
        Console.WriteLine($"Function:\n{shared["function_code"]}");
        
        var testResults = shared["test_results"] as IList<Dictionary<string, object>> ?? new List<Dictionary<string, object>>();
        var passed = testResults.Count(r => r.TryGetValue("passed", out var p) && p is true);
        Console.WriteLine($"Test Results: {passed}/{testResults.Count} passed");
    }
}
