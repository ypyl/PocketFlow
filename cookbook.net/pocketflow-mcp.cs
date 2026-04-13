#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Text;
using System.Text.RegularExpressions;
using System.Numerics;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class ToolInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, ToolParam> InputSchema { get; set; } = new();
}

class ToolParam
{
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
}

class GetToolsNode : Node<TShared, string, List<ToolInfo>>
{
    public override Task<string?> Prep(TShared shared)
    {
        return Task.FromResult<string?>("simple_server.py");
    }

    public override Task<List<ToolInfo>> Exec(string serverPath)
    {
        Console.WriteLine("[GetToolsNode] Getting available tools...");
        return Task.FromResult(GetMockTools());
    }

    public override Task<string?> Post(TShared shared, string? prepRes, List<ToolInfo>? execRes)
    {
        shared["tools"] = execRes ?? new List<ToolInfo>();
        
        var toolInfo = new StringBuilder();
        for (int i = 0; i < execRes?.Count; i++)
        {
            var tool = execRes[i];
            toolInfo.AppendLine($"[{i + 1}] {tool.Name}");
            toolInfo.AppendLine($"  Description: {tool.Description}");
            toolInfo.AppendLine("  Parameters:");
            
            foreach (var kvp in tool.InputSchema)
            {
                var reqStatus = kvp.Value.Required ? "(Required)" : "(Optional)";
                toolInfo.AppendLine($"    - {kvp.Key} ({kvp.Value.Type}): {reqStatus}");
            }
        }
        
        shared["tool_info"] = toolInfo.ToString();
        return Task.FromResult<string?>("decide");
    }

    private List<ToolInfo> GetMockTools()
    {
        return new List<ToolInfo>
        {
            new()
            {
                Name = "add",
                Description = "Add two numbers together",
                InputSchema = new Dictionary<string, ToolParam>
                {
                    ["a"] = new() { Type = "integer", Required = true },
                    ["b"] = new() { Type = "integer", Required = true }
                }
            },
            new()
            {
                Name = "subtract",
                Description = "Subtract b from a",
                InputSchema = new Dictionary<string, ToolParam>
                {
                    ["a"] = new() { Type = "integer", Required = true },
                    ["b"] = new() { Type = "integer", Required = true }
                }
            },
            new()
            {
                Name = "multiply",
                Description = "Multiply two numbers together",
                InputSchema = new Dictionary<string, ToolParam>
                {
                    ["a"] = new() { Type = "integer", Required = true },
                    ["b"] = new() { Type = "integer", Required = true }
                }
            },
            new()
            {
                Name = "divide",
                Description = "Divide a by b",
                InputSchema = new Dictionary<string, ToolParam>
                {
                    ["a"] = new() { Type = "integer", Required = true },
                    ["b"] = new() { Type = "integer", Required = true }
                }
            }
        };
    }
}

class DecideToolNode : Node<TShared, string, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        var toolInfo = shared.TryGetValue("tool_info", out var t) ? t as string ?? "" : "";
        var question = shared.TryGetValue("question", out var q) ? q as string ?? "" : "";
        
        var prompt = $@"### CONTEXT
You are an assistant that can use tools via Model Context Protocol (MCP).

### ACTION SPACE
{toolInfo}

### TASK
Answer this question: ""{question}""

## NEXT ACTION
Analyze the question, extract any numbers or parameters, and decide which tool to use.
Return your response in this format:

```yaml
thinking: |
    <your step-by-step reasoning about what the question is asking and what numbers to extract>
tool: <name of the tool to use>
reason: <why you chose this tool>
parameters:
    <parameter_name>: <parameter_value>
    <parameter_name>: <parameter_value>
```
IMPORTANT: 
1. Extract numbers from the question properly
2. Use proper indentation (4 spaces) for multi-line fields
3. Use the | character for multi-line text fields
";
        return Task.FromResult<string?>(prompt);
    }

    public override Task<string> Exec(string prompt)
    {
        Console.WriteLine("[DecideToolNode] Analyzing question and deciding which tool to use...");
        var question = prompt.Contains("plus") ? "add" : prompt.Contains("minus") ? "subtract" : 
                       prompt.Contains("times") ? "multiply" : prompt.Contains("divided") ? "divide" : "add";
        return Task.FromResult(MockLLMDecision(prompt, question));
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string execRes)
    {
        try
        {
            var yamlStr = execRes.Split("```yaml")[1].Split("```")[0].Trim();
            var decision = ParseYamlResponse(yamlStr);
            
            shared["tool_name"] = decision.GetValueOrDefault("tool", "unknown");
            shared["parameters"] = decision.GetValueOrDefault("parameters", new Dictionary<string, object>());
            shared["thinking"] = decision.GetValueOrDefault("thinking", "");
            
            Console.WriteLine($"[DecideToolNode] Selected tool: {shared["tool_name"]}");
            Console.WriteLine($"[DecideToolNode] Extracted parameters: {shared["parameters"]}");
            
            return Task.FromResult<string?>("execute");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[DecideToolNode] Error parsing LLM response: {e.Message}");
            Console.WriteLine($"Raw response: {execRes}");
            return Task.FromResult<string?>(null);
        }
    }

    private Dictionary<string, object> ParseYamlResponse(string yaml)
    {
        var result = new Dictionary<string, object>();
        var lines = yaml.Split('\n');
        
        string? currentKey = null;
        var currentValue = new StringBuilder();
        var inMultiline = false;
        var inParameters = false;
        var paramDict = new Dictionary<string, object>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (trimmed.StartsWith("#") || string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (trimmed.StartsWith("thinking:"))
            {
                inMultiline = true;
                inParameters = false;
                currentKey = "thinking";
                var content = trimmed.Substring("thinking:".Length).Trim();
                if (content == "|" || content == ">")
                    continue;
                if (!string.IsNullOrEmpty(content))
                    currentValue.AppendLine(content);
            }
            else if (trimmed.StartsWith("tool:"))
            {
                inMultiline = false;
                currentKey = "tool";
                result["tool"] = trimmed.Substring("tool:".Length).Trim();
            }
            else if (trimmed.StartsWith("reason:"))
            {
                result["reason"] = trimmed.Substring("reason:".Length).Trim();
            }
            else if (trimmed.StartsWith("parameters:"))
            {
                inMultiline = false;
                inParameters = true;
            }
            else if (inMultiline && currentKey != null)
            {
                if (trimmed == "---" || (trimmed.StartsWith("tool:") || trimmed.StartsWith("reason:") || trimmed.StartsWith("parameters:")))
                {
                    if (currentKey == "thinking")
                    {
                        result[currentKey] = currentValue.ToString().Trim();
                        currentValue.Clear();
                    }
                    inMultiline = false;
                    if (trimmed.StartsWith("tool:"))
                    {
                        currentKey = "tool";
                        result["tool"] = trimmed.Substring("tool:".Length).Trim();
                    }
                    else if (trimmed.StartsWith("reason:"))
                    {
                        result["reason"] = trimmed.Substring("reason:".Length).Trim();
                    }
                    else if (trimmed.StartsWith("parameters:"))
                    {
                        inParameters = true;
                    }
                }
                else
                {
                    currentValue.AppendLine(line);
                }
            }
            else if (inParameters && trimmed.Contains(":"))
            {
                var colonIndex = trimmed.IndexOf(':');
                var key = trimmed.Substring(0, colonIndex).Trim();
                var value = trimmed.Substring(colonIndex + 1).Trim();
                
                if (BigInteger.TryParse(value, out var bigIntVal))
                    paramDict[key] = bigIntVal;
                else if (long.TryParse(value, out var longVal))
                    paramDict[key] = longVal;
                else if (double.TryParse(value, out var doubleVal))
                    paramDict[key] = doubleVal;
                else
                    paramDict[key] = value.Trim('"', ' ');
            }
        }

        if (currentKey == "thinking" && currentValue.Length > 0)
        {
            result[currentKey] = currentValue.ToString().Trim();
        }

        if (paramDict.Count > 0)
        {
            result["parameters"] = paramDict;
        }

        return result;
    }

    private string MockLLMDecision(string prompt, string suggestedTool)
    {
        BigInteger a = 0, b = 0;
        
        var questionMatch = Regex.Match(prompt, @"Answer this question:\s*""([^""]+)""");
        if (questionMatch.Success)
        {
            var question = questionMatch.Groups[1].Value;
            var numbers = ExtractNumbers(question);
            if (numbers.Count >= 2)
            {
                a = numbers[^2];
                b = numbers[^1];
            }
            else if (numbers.Count == 1)
            {
                a = numbers[0];
                b = 0;
            }
        }
        
        if (prompt.Contains("plus") || prompt.Contains("+"))
        {
            return $@"```yaml
thinking: |
    The question asks to add two numbers. I need to extract the numbers from the question.
    
    From the question: ""{prompt.Split('\n').FirstOrDefault()?.Replace("### TASK", "").Replace("Answer this question:", "").Trim() ?? ""}""
    
    The first number is {a} and the second number is {b}.
    I will use the 'add' tool to compute the sum.
tool: add
reason: The question asks for the sum of two numbers
parameters:
    a: {a}
    b: {b}
```";
        }
        
        if (prompt.Contains("minus") || prompt.Contains("-"))
        {
            return $@"```yaml
thinking: |
    The question asks to subtract two numbers.
    
    Extracting numbers: {a} minus {b}
    
    I will use the 'subtract' tool.
tool: subtract
reason: The question asks for the difference of two numbers
parameters:
    a: {a}
    b: {b}
```";
        }
        
        if (prompt.Contains("times") || prompt.Contains("*"))
        {
            return $@"```yaml
thinking: |
    The question asks to multiply two numbers.
    
    Extracting numbers: {a} times {b}
    
    I will use the 'multiply' tool.
tool: multiply
reason: The question asks for the product of two numbers
parameters:
    a: {a}
    b: {b}
```";
        }
        
        if (prompt.Contains("divided"))
        {
            return $@"```yaml
thinking: |
    The question asks to divide two numbers.
    
    Extracting numbers: {a} divided by {b}
    
    I will use the 'divide' tool.
tool: divide
reason: The question asks for the quotient of two numbers
parameters:
    a: {a}
    b: {b}
```";
        }

        return $@"```yaml
thinking: |
    Analyzing the question to determine the appropriate tool.
tool: add
reason: Default to add for arithmetic questions
parameters:
    a: {a}
    b: {b}
```";
    }

    private List<BigInteger> ExtractNumbers(string text)
    {
        var matches = Regex.Matches(text, @"\d+");
        return matches.Select(m => BigInteger.Parse(m.Value)).ToList();
    }
}

class ExecuteToolNode : Node<TShared, (string ToolName, Dictionary<string, object> Parameters), string>
{
    public override Task<(string ToolName, Dictionary<string, object> Parameters)> Prep(TShared shared)
    {
        var toolName = shared.TryGetValue("tool_name", out var t) ? t as string ?? "" : "";
        var parameters = shared.TryGetValue("parameters", out var p) ? p as Dictionary<string, object> ?? new() : new();
        return Task.FromResult<(string, Dictionary<string, object>)>((toolName, parameters));
    }

    public override Task<string> Exec((string ToolName, Dictionary<string, object> Parameters) input)
    {
        var (toolName, parameters) = input;
        
        Console.WriteLine($"[ExecuteToolNode] Executing tool '{toolName}' with parameters: {string.Join(", ", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        
        var result = MockExecuteTool(toolName, parameters);
        
        return Task.FromResult(result);
    }

    public override Task<string?> Post(TShared shared, (string ToolName, Dictionary<string, object> Parameters) prepRes, string execRes)
    {
        Console.WriteLine($"\n[ExecuteToolNode] Final Answer: {execRes}");
        return Task.FromResult<string?>("done");
    }

    private string MockExecuteTool(string toolName, Dictionary<string, object> parameters)
    {
        var a = parameters.GetValueOrDefault("a", BigInteger.Zero);
        var b = parameters.GetValueOrDefault("b", BigInteger.Zero);
        
        var aVal = a is BigInteger biA ? biA : BigInteger.Parse(a.ToString() ?? "0");
        var bVal = b is BigInteger biB ? biB : BigInteger.Parse(b.ToString() ?? "0");

        return toolName switch
        {
            "add" => $"The sum of {a} and {b} is {aVal + bVal}",
            "subtract" => $"The difference of {a} and {b} is {aVal - bVal}",
            "multiply" => $"The product of {a} and {b} is {aVal * bVal}",
            "divide" => bVal != 0 ? $"The quotient of {a} divided by {b} is {aVal / bVal}" : "Error: Division by zero is not allowed",
            _ => $"Unknown tool: {toolName}"
        };
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var question = "What is 982713504867129384651 plus 73916582047365810293746529?";
        
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                question = args[i].Substring(2);
                break;
            }
        }

        Console.WriteLine($"Processing question: {question}");
        
        var shared = new Dictionary<string, object>
        {
            ["question"] = question
        };

        var getToolsNode = new GetToolsNode();
        var decideNode = new DecideToolNode();
        var executeNode = new ExecuteToolNode();

        getToolsNode.On("decide").To(decideNode);
        decideNode.On("execute").To(executeNode);

        var flow = new Flow<TShared>(getToolsNode);
        await flow.Run(shared);
    }
}
