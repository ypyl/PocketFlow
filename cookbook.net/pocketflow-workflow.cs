#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Text.RegularExpressions;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class GenerateOutlineNode : Node<TShared, string, Dictionary<string, object>>
{
    public override Task<string?> Prep(TShared shared)
    {
        var topic = shared.TryGetValue("topic", out var t) ? t as string ?? "Unknown" : "Unknown";
        return Task.FromResult<string?>(topic);
    }

    public override Task<Dictionary<string, object>> Exec(string topic)
    {
        var prompt = $@"
Create a simple outline for an article about {topic}.
Include at most 3 main sections (no subsections).

Output the sections in YAML format as shown below:

```yaml
sections:
    - |
        First section 
    - |
        Second section
    - |
        Third section
```";

        Console.WriteLine($"[GenerateOutline] Creating outline for topic: {topic}");
        var response = MockCallLLM(prompt);
        Console.WriteLine("[GenerateOutline] Outline generated");
        
        var yamlStr = response.Split("```yaml")[1].Split("```")[0].Trim();
        var result = ParseYamlOutline(yamlStr);
        
        return Task.FromResult(result);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, Dictionary<string, object>? execRes)
    {
        if (execRes == null) return Task.FromResult<string?>("default");
        
        shared["outline_yaml"] = execRes;
        
        var sections = execRes["sections"] as List<string>;
        shared["sections"] = sections;
        
        var formattedOutline = string.Join("\n", sections!.Select((s, i) => $"{i+1}. {s}"));
        shared["outline"] = formattedOutline;
        
        Console.WriteLine("\n===== OUTLINE (YAML) =====\n");
        foreach (var kvp in execRes)
        {
            Console.WriteLine($"{kvp.Key}: {kvp.Value}");
        }
        Console.WriteLine("\n===== PARSED OUTLINE =====\n");
        Console.WriteLine(formattedOutline);
        Console.WriteLine("\n=========================\n");
        
        return Task.FromResult<string?>("default");
    }

    private static Dictionary<string, object> ParseYamlOutline(string yaml)
    {
        var sections = new List<string>();
        var lines = yaml.Split('\n');
        bool inSections = false;
        var currentSection = new List<string>();
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == "sections:" || trimmed == "sections:")
            {
                inSections = true;
                continue;
            }
            if (inSections && (trimmed.StartsWith("- |") || trimmed.StartsWith("- ")))
            {
                if (currentSection.Count > 0)
                {
                    sections.Add(string.Join(" ", currentSection).Trim());
                    currentSection = new List<string>();
                }
                var content = trimmed.StartsWith("- |") ? trimmed[3..].Trim() : trimmed[2..].Trim();
                currentSection.Add(content);
            }
            else if (inSections && currentSection.Count > 0 && (trimmed.Length > 0 && !trimmed.StartsWith("#")))
            {
                currentSection.Add(trimmed);
            }
        }
        if (currentSection.Count > 0)
        {
            sections.Add(string.Join(" ", currentSection).Trim());
        }
        
        return new Dictionary<string, object> { ["sections"] = sections };
    }

    private static string MockCallLLM(string prompt)
    {
        return @"```yaml
sections:
    - Introduction to AI Safety
    - Key Challenges in AI Safety
    - Future Directions
```";
    }
}

class WriteSimpleContentNode : BatchNode<TShared, string, (string Section, string Content)>
{
    public WriteSimpleContentNode(IDictionary<string, object>? defaultParams = null, int maxRetries = 1, double wait = 0, bool enableParallel = false)
        : base(defaultParams, maxRetries, wait, enableParallel)
    {
    }

    public override Task<IEnumerable<string>?> Prep(TShared shared)
    {
        var sections = shared.TryGetValue("sections", out var s) ? s as List<string> ?? new List<string>() : new List<string>();
        return Task.FromResult<IEnumerable<string>?>(sections);
    }

    public override Task<(string Section, string Content)> ExecItem(string section)
    {
        var prompt = $@"
Write a short paragraph (MAXIMUM 100 WORDS) about this section:

{section}

Requirements:
- Explain the idea in simple, easy-to-understand terms
- Use everyday language, avoiding jargon
- Keep it very concise (no more than 100 words)
- Include one brief example or analogy
";

        Console.WriteLine($"[WriteSimpleContent] Writing content for section: {section}");
        var content = MockCallLLM(prompt);
        
        return Task.FromResult((section, content));
    }

    public override Task<string?> Post(TShared shared, IEnumerable<string>? prepRes, IList<(string Section, string Content)>? execRes)
    {
        if (execRes == null) return Task.FromResult<string?>("default");
        
        var sectionContents = new Dictionary<string, string>();
        var allSectionsContent = new List<string>();
        
        foreach (var (section, content) in execRes)
        {
            sectionContents[section] = content;
            allSectionsContent.Add($"## {section}\n\n{content}\n");
            Console.WriteLine($"[WriteSimpleContent] Completed section: {section}");
        }
        
        var draft = string.Join("\n", allSectionsContent);
        
        shared["section_contents"] = sectionContents;
        shared["draft"] = draft;
        
        Console.WriteLine("\n===== SECTION CONTENTS =====\n");
        foreach (var kvp in sectionContents)
        {
            Console.WriteLine($"--- {kvp.Key} ---");
            Console.WriteLine(kvp.Value);
            Console.WriteLine();
        }
        Console.WriteLine("===========================\n");
        
        return Task.FromResult<string?>("default");
    }

    private static string MockCallLLM(string prompt)
    {
        if (prompt.Contains("Introduction to AI Safety"))
        {
            return "AI safety is about ensuring that artificial intelligence systems behave as intended and don't cause harm. Just like safety features in cars or airplanes, AI safety research aims to prevent accidents and unintended consequences. For example, we want AI to understand that 'make a pizza' means adding cheese and toppings, not literally creating a pizza from scratch.";
        }
        if (prompt.Contains("Key Challenges"))
        {
            return "One major challenge in AI safety is the problem of specification. We need to tell AI exactly what we want it to do, which is harder than it sounds. Imagine telling someone to 'make a cake' without explaining what a cake is, what ingredients are needed, or what steps to follow. AI systems face similar challenges when trying to understand our intentions.";
        }
        if (prompt.Contains("Future Directions"))
        {
            return "The future of AI safety lies in developing systems that can learn and improve while staying aligned with human values. Researchers are exploring ways to make AI more transparent, interpretable, and robust. Think of it like teaching a child - we want AI to grow smarter but still listen to its teachers and follow the rules.";
        }
        return "This is a mock content section. In a real application, this would be generated by an LLM API call.";
    }
}

class ApplyStyleNode : Node<TShared, string, string>
{
    public override Task<string?> Prep(TShared shared)
    {
        var draft = shared.TryGetValue("draft", out var d) ? d as string ?? "" : "";
        return Task.FromResult<string?>(draft);
    }

    public override Task<string?> Exec(string draft)
    {
        var prompt = $@"
Rewrite the following draft in a conversational, engaging style:

{draft}

Make it:
- Conversational and warm in tone
- Include rhetorical questions that engage the reader
- Add analogies and metaphors where appropriate
- Include a strong opening and conclusion
";

        Console.WriteLine("[ApplyStyle] Applying conversational style...");
        var result = MockCallLLM(prompt);
        Console.WriteLine("[ApplyStyle] Style applied");
        
        return Task.FromResult<string?>(result);
    }

    public override Task<string?> Post(TShared shared, string? prepRes, string? execRes)
    {
        if (execRes == null) return Task.FromResult<string?>("default");
        
        shared["final_article"] = execRes;
        
        Console.WriteLine("\n===== FINAL ARTICLE =====\n");
        Console.WriteLine(execRes);
        Console.WriteLine("\n========================\n");
        
        return Task.FromResult<string?>("default");
    }

    private static string MockCallLLM(string prompt)
    {
        return @"Have you ever wondered how we can make sure artificial intelligence stays safe as it becomes more powerful? 

AI safety might sound like something from a sci-fi movie, but it's actually one of the most important fields in modern technology. Think of it like teaching a child - we want AI to grow smarter but still listen to its teachers and follow the rules.

The challenge is that AI systems can sometimes misunderstand what we want them to do. Just like a student who misinterprets homework instructions, AI can take our requests too literally or too loosely. That's why researchers are working hard to develop systems that truly understand human intentions.

The future of AI safety isn't just about preventing disasters - it's about building trust. When we climb a mountain, we don't just strap on boots and start hiking. We prepare, we plan, and we take precautions. AI development needs the same careful approach.

Ready to explore this fascinating field further? The journey has only just begun.";
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var topic = args.Length > 0 ? string.Join(" ", args) : "AI Safety";
        
        var shared = new Dictionary<string, object>
        {
            ["topic"] = topic
        };

        Console.WriteLine($"\n=== Starting Article Workflow on Topic: {topic} ===\n");

        var outlineNode = new GenerateOutlineNode();
        var writeNode = new WriteSimpleContentNode();
        var styleNode = new ApplyStyleNode();

        outlineNode.Next(writeNode);
        writeNode.Next(styleNode);

        var flow = new Flow<TShared>(outlineNode);
        await flow.Run(shared);

        Console.WriteLine("\n=== Workflow Completed ===\n");
        Console.WriteLine($"Topic: {shared["topic"]}");
        Console.WriteLine($"Outline Length: {shared["outline"].ToString()?.Length ?? 0} characters");
        Console.WriteLine($"Draft Length: {shared["draft"].ToString()?.Length ?? 0} characters");
        Console.WriteLine($"Final Article Length: {shared["final_article"].ToString()?.Length ?? 0} characters");
    }
}
