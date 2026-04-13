#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Text;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class ChainOfThoughtNode : Node<TShared, Dictionary<string, object>, Dictionary<string, object>>
{
    private int _currentSectionIndex;
    private List<string> _sections = new();

    public override Task<Dictionary<string, object>?> Prep(TShared shared)
    {
        var problem = shared.TryGetValue("problem", out var p) ? p as string ?? "" : "";
        var thoughts = shared.TryGetValue("thoughts", out var t) ? t as List<Dictionary<string, object>> ?? new() : new();
        var currentThoughtNumber = shared.TryGetValue("current_thought_number", out var c) ? (int)c : 0;

        shared["current_thought_number"] = currentThoughtNumber + 1;

        var thoughtsText = "";
        List<Dictionary<string, object>>? lastPlanStructure = null;

        if (thoughts.Count > 0)
        {
            var thoughtsTextList = new List<string>();
            for (int i = 0; i < thoughts.Count; i++)
            {
                var t_data = thoughts[i];
                var thoughtBlock = $"Thought {t_data.GetValueOrDefault("thought_number", i + 1)}:\n";
                var thinking = t_data.GetValueOrDefault("current_thinking", "N/A")?.ToString() ?? "N/A";
                thoughtBlock += $"  Thinking:\n{IndentText(thinking, "    ")}\n";

                var planList = t_data.GetValueOrDefault("planning", new List<object>()) as List<object>;
                if (planList != null)
                {
                    var planStrFormatted = FormatPlan(planList, 2);
                    thoughtBlock += $"  Plan Status After Thought {t_data.GetValueOrDefault("thought_number", i + 1)}:\n{planStrFormatted}";
                }

                if (i == thoughts.Count - 1)
                {
                    lastPlanStructure = planList as List<Dictionary<string, object>>;
                }

                thoughtsTextList.Add(thoughtBlock);
            }
            thoughtsText = string.Join("\n--------------------\n", thoughtsTextList);
        }
        else
        {
            thoughtsText = "No previous thoughts yet.";
            lastPlanStructure = new List<Dictionary<string, object>>
            {
                new() { ["description"] = "Understand the problem", ["status"] = "Pending" },
                new() { ["description"] = "Develop a high-level plan", ["status"] = "Pending" },
                new() { ["description"] = "Conclusion", ["status"] = "Pending" }
            };
        }

        var lastPlanText = FormatPlanForPrompt(lastPlanStructure ?? new());

        return Task.FromResult<Dictionary<string, object>?>(new Dictionary<string, object>
        {
            ["problem"] = problem,
            ["thoughts_text"] = thoughtsText,
            ["last_plan_text"] = lastPlanText,
            ["last_plan_structure"] = lastPlanStructure ?? new(),
            ["current_thought_number"] = currentThoughtNumber + 1,
            ["is_first_thought"] = thoughts.Count == 0
        });
    }

    public override Task<Dictionary<string, object>> Exec(Dictionary<string, object> prepRes)
    {
        var problem = prepRes["problem"]?.ToString() ?? "";
        var thoughtsText = prepRes["thoughts_text"]?.ToString() ?? "";
        var lastPlanText = prepRes["last_plan_text"]?.ToString() ?? "";
        var currentThoughtNumber = (int)prepRes["current_thought_number"];
        var isFirstThought = (bool)prepRes["is_first_thought"];

        var instructionBase = $@"
Your task is to generate the next thought (Thought {currentThoughtNumber}).

Instructions:
1.  **Evaluate Previous Thought:** If not the first thought, start `current_thinking` by evaluating Thought {currentThoughtNumber - 1}. State: ""Evaluation of Thought {currentThoughtNumber - 1}: [Correct/Minor Issues/Major Error - explain]"". Address errors first.
2.  **Execute Step:** Execute the first step in the plan with `status: Pending`.
3.  **Maintain Plan (Structure):** Generate an updated `planning` list. Each item should be a dictionary with keys: `description` (string), `status` (string: ""Pending"", ""Done"", ""Verification Needed""), and optionally `result` (string, concise summary when Done) or `mark` (string, reason for Verification Needed). Sub-steps are represented by a `sub_steps` key containing a *list* of these dictionaries.
4.  **Update Current Step Status:** In the updated plan, change the `status` of the executed step to ""Done"" and add a `result` key with a concise summary. If verification is needed based on evaluation, change status to ""Verification Needed"" and add a `mark`.
5.  **Refine Plan (Sub-steps):** If a ""Pending"" step is complex, add a `sub_steps` key to its dictionary containing a list of new step dictionaries (status: ""Pending"") breaking it down. Keep the parent step's status ""Pending"" until all sub-steps are ""Done"".
6.  **Refine Plan (Errors):** Modify the plan logically based on evaluation findings (e.g., change status, add correction steps).
7.  **Final Step:** Ensure the plan progresses towards a final step dictionary like `{{'description': ""Conclusion"", 'status': ""Pending""}}`.
8.  **Termination:** Set `next_thought_needed` to `false` ONLY when executing the step with `description: ""Conclusion""`.
";

        string instructionContext;
        if (isFirstThought)
        {
            instructionContext = @"
**This is the first thought:** Create an initial plan as a list of dictionaries (keys: description, status). Include sub-steps via the `sub_steps` key if needed. Then, execute the first step in `current_thinking` and provide the updated plan (marking step 1 `status: Done` with a `result`).
";
        }
        else
        {
            instructionContext = $@"
**Previous Plan (Simplified View):**
{lastPlanText}

Start `current_thinking` by evaluating Thought {currentThoughtNumber - 1}. Then, proceed with the first step where `status: Pending`. Update the plan structure (list of dictionaries) reflecting evaluation, execution, and refinements.
";
        }

        var instructionFormat = @"
Format your response ONLY as a YAML structure enclosed in ```yaml ... ```:
```yaml
current_thinking: |
  # Evaluation of Thought N: [Assessment] ... (if applicable)
  # Thinking for the current step...
planning:
  # List of dictionaries (keys: description, status, Optional[result, mark, sub_steps])
  - description: ""Step 1""
    status: ""Done""
    result: ""Concise result summary""
  - description: ""Step 2 Complex Task"" # Now broken down
    status: ""Pending"" # Parent remains Pending
    sub_steps:
      - description: ""Sub-task 2a""
        status: ""Pending""
      - description: ""Sub-task 2b""
        status: ""Verification Needed""
        mark: ""Result from Thought X seems off""
  - description: ""Step 3""
    status: ""Pending""
  - description: ""Conclusion""
    status: ""Pending""
next_thought_needed: true # Set to false ONLY when executing the Conclusion step.
";
        var prompt = $@"
You are a meticulous AI assistant solving a complex problem step-by-step using a structured plan. You critically evaluate previous steps, refine the plan with sub-steps if needed, and handle errors logically. Use the specified YAML dictionary structure for the plan.

Problem: {problem}

Previous thoughts:
{thoughtsText}
--------------------
{instructionBase}
{instructionContext}
{instructionFormat}
";

        Console.WriteLine($"[ChainOfThought] Generating thought {currentThoughtNumber}...");
        var response = MockCallLLM(prompt, currentThoughtNumber);

        var yamlStr = response.Split("```yaml")[1].Split("```")[0].Trim();
        var thoughtData = ParseYamlResponse(yamlStr);
        thoughtData["thought_number"] = currentThoughtNumber;

        return Task.FromResult(thoughtData);
    }

    public override Task<string?> Post(TShared shared, Dictionary<string, object>? prepRes, Dictionary<string, object>? execRes)
    {
        if (execRes == null) return Task.FromResult<string?>("default");

        if (!shared.ContainsKey("thoughts"))
            shared["thoughts"] = new List<Dictionary<string, object>>();
        
        ((List<Dictionary<string, object>>)shared["thoughts"]).Add(execRes);

        var planList = execRes.GetValueOrDefault("planning", new List<object>()) as List<object> ?? new();
        var planStrFormatted = FormatPlan(planList, 1);

        var thoughtNum = execRes.GetValueOrDefault("thought_number", "N/A");
        var currentThinking = execRes.GetValueOrDefault("current_thinking", "Error: Missing thinking content.")?.ToString() ?? "";

        var isConclusion = false;
        if (execRes.GetValueOrDefault("next_thought_needed", true) is bool nextNeeded && !nextNeeded)
        {
            isConclusion = true;
        }

        if (isConclusion)
        {
            shared["solution"] = currentThinking;
            Console.WriteLine($"\nThought {thoughtNum} (Conclusion):");
            Console.WriteLine(IndentText(currentThinking, "  "));
            Console.WriteLine("\nFinal Plan Status:");
            Console.WriteLine(IndentText(planStrFormatted, "  "));
            Console.WriteLine("\n=== FINAL SOLUTION ===");
            Console.WriteLine(currentThinking);
            Console.WriteLine("======================\n");
            return Task.FromResult<string?>("end");
        }

        Console.WriteLine($"\nThought {thoughtNum}:");
        Console.WriteLine(IndentText(currentThinking, "  "));
        Console.WriteLine("\nCurrent Plan Status:");
        Console.WriteLine(IndentText(planStrFormatted, "  "));
        Console.WriteLine("-" * 50);

        return Task.FromResult<string?>("continue");
    }

    private static string IndentText(string text, string indent)
    {
        return string.Join("\n", text.Split('\n').Select(line => indent + line));
    }

    private static string FormatPlan(List<object> planItems, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 2);
        var output = new List<string>();

        foreach (var item in planItems)
        {
            if (item is Dictionary<string, object> dict)
            {
                var status = dict.GetValueOrDefault("status", "Unknown")?.ToString() ?? "Unknown";
                var desc = dict.GetValueOrDefault("description", "No description")?.ToString() ?? "No description";
                var result = dict.GetValueOrDefault("result", "")?.ToString() ?? "";
                var mark = dict.GetValueOrDefault("mark", "")?.ToString() ?? "";

                var line = $"{indent}- [{status}] {desc}";
                if (!string.IsNullOrEmpty(result))
                    line += $": {result}";
                if (!string.IsNullOrEmpty(mark))
                    line += $" ({mark})";
                output.Add(line);

                var subSteps = dict.GetValueOrDefault("sub_steps") as List<object>;
                if (subSteps != null)
                {
                    output.Add(FormatPlan(subSteps, indentLevel + 1));
                }
            }
            else if (item is string s)
            {
                output.Add($"{indent}- {s}");
            }
            else
            {
                output.Add($"{indent}- {item?.ToString() ?? "null"}");
            }
        }

        return string.Join("\n", output);
    }

    private static string FormatPlanForPrompt(List<Dictionary<string, object>> planItems)
    {
        var output = new List<string>();
        foreach (var item in planItems)
        {
            var status = item.GetValueOrDefault("status", "Unknown")?.ToString() ?? "Unknown";
            var desc = item.GetValueOrDefault("description", "No description")?.ToString() ?? "No description";
            output.Add($"  - [{status}] {desc}");
        }
        return string.Join("\n", output);
    }

    private Dictionary<string, object> ParseYamlResponse(string yaml)
    {
        var result = new Dictionary<string, object>();
        var lines = yaml.Split('\n').Select(l => l.Trim()).ToArray();
        
        string? currentKey = null;
        var currentValue = new StringBuilder();
        var inMultiline = false;
        var multilineKey = "";

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            if (line.Contains(":"))
            {
                if (inMultiline && currentKey != null)
                {
                    result[currentKey] = currentValue.ToString().Trim();
                    currentValue.Clear();
                    inMultiline = false;
                }

                var colonIndex = line.IndexOf(':');
                var key = line.Substring(0, colonIndex).Trim();
                var value = line.Substring(colonIndex + 1).Trim();

                if (value == "|" || value == ">")
                {
                    inMultiline = true;
                    multilineKey = key;
                    currentKey = key;
                }
                else if (!string.IsNullOrEmpty(value) && value != "\"\"")
                {
                    result[key] = value.Trim('"', '|', '>');
                }
                else
                {
                    currentKey = key;
                    currentValue.Clear();
                }
            }
            else if (inMultiline)
            {
                currentValue.AppendLine(line);
            }
        }

        if (inMultiline && currentKey != null)
        {
            result[currentKey] = currentValue.ToString().Trim();
        }

        if (result.ContainsKey("planning") && result["planning"] is string planStr)
        {
            result["planning"] = ParsePlanningList(planStr);
        }

        return result;
    }

    private List<Dictionary<string, object>> ParsePlanningList(string planStr)
    {
        var result = new List<Dictionary<string, object>>();
        var lines = planStr.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToArray();
        
        Dictionary<string, object>? current = null;
        var inSubSteps = false;
        var subSteps = new List<Dictionary<string, object>>();

        foreach (var line in lines)
        {
            if (line.StartsWith("- description:"))
            {
                if (current != null)
                {
                    if (inSubSteps)
                        subSteps.Add(current);
                    else
                        result.Add(current);
                }
                current = new Dictionary<string, object>();
                var desc = line.Substring("- description:".Length).Trim().Trim('"', '|');
                current["description"] = desc;
                inSubSteps = false;
                subSteps.Clear();
            }
            else if (line.StartsWith("status:"))
            {
                var status = line.Substring("status:".Length).Trim().Trim('"', '|');
                if (current != null) current["status"] = status;
            }
            else if (line.StartsWith("result:"))
            {
                var res = line.Substring("result:".Length).Trim().Trim('"', '|');
                if (current != null) current["result"] = res;
            }
            else if (line.StartsWith("mark:"))
            {
                var mark = line.Substring("mark:".Length).Trim().Trim('"', '|');
                if (current != null) current["mark"] = mark;
            }
            else if (line.StartsWith("sub_steps:"))
            {
                inSubSteps = true;
                if (current != null) current["sub_steps"] = subSteps;
            }
        }

        if (current != null)
        {
            if (inSubSteps)
                subSteps.Add(current);
            else
                result.Add(current);
        }

        return result;
    }

    private static string MockCallLLM(string prompt, int thoughtNumber)
    {
        if (thoughtNumber == 1)
        {
            return @"```yaml
current_thinking: |
  Let me analyze this probability problem step by step.
  
  First, I need to understand the problem: we roll a fair die until we see the sequence 3, 4, 5 in that order consecutively. We want the probability that the total number of rolls is odd.
  
  Let me break this down into manageable steps.
planning:
  - description: ""Understand the problem""
    status: ""Done""
    result: ""Problem involves probability of odd number of rolls until sequence 3,4,5 appears""
  - description: ""Develop a high-level plan""
    status: ""Pending""
    sub_steps:
      - description: ""Calculate probability of getting 3,4,5 in first 3 rolls""
        status: ""Pending""
      - description: ""Calculate probability of getting odd total rolls""
        status: ""Pending""
      - description: ""Combine and get final probability""
        status: ""Pending""
  - description: ""Conclusion""
    status: ""Pending""
next_thought_needed: true
```";
        }

        if (prompt.Contains("Verification Needed") || thoughtNumber >= 3)
        {
            return @"```yaml
current_thinking: |
  Based on my analysis, I can now provide the final solution.
  
  Let P be the probability that we roll a 3, then 4, then 5 consecutively.
  The probability of getting 3-4-5 in any three consecutive rolls is (1/6)^3 = 1/216.
  
  Let Q be the probability that the number of rolls is odd.
  
  After careful consideration, the probability is 4/7.
planning:
  - description: ""Understand the problem""
    status: ""Done""
    result: ""Problem involves probability of odd number of rolls until sequence 3,4,5 appears""
  - description: ""Develop a high-level plan""
    status: ""Done""
    result: ""Used states and recursion to solve""
  - description: ""Conclusion""
    status: ""Done""
    result: ""Final probability is 4/7""
next_thought_needed: false
```";
        }

        return @"```yaml
current_thinking: |
  Building on the previous thought, let me continue developing the solution.
  
  We can use a state-based approach where each state represents the last few rolls we've seen.
planning:
  - description: ""Understand the problem""
    status: ""Done""
    result: ""Problem involves probability of odd number of rolls""
  - description: ""Develop a high-level plan""
    status: ""Done""
    result: ""Using Markov chain approach with states S0, S3, S34, S345""
  - description: ""Conclusion""
    status: ""Pending""
next_thought_needed: true
```";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var question = args.Length > 0 && args[0].StartsWith("--") 
            ? args[0].Substring(2) 
            : "You keep rolling a fair die until you roll three, four, five in that order consecutively on three rolls. What is the probability that you roll the die an odd number of times?";

        Console.WriteLine($"Processing question: {question}");

        var shared = new Dictionary<string, object>
        {
            ["problem"] = question,
            ["thoughts"] = new List<Dictionary<string, object>>(),
            ["current_thought_number"] = 0,
            ["solution"] = null
        };

        var cotNode = new ChainOfThoughtNode();
        cotNode.On("continue").To(cotNode);

        var flow = new Flow<TShared>(cotNode);
        await flow.RunAsync(shared);
    }
}
