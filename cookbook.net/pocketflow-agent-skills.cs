#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Text.RegularExpressions;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class SelectSkill : Node<TShared, Dictionary<string, object>?, (string SkillName, string SkillContent)?>
{
    public override Task<Dictionary<string, object>?> Prep(TShared shared)
    {
        var skills = LoadSkills();
        return Task.FromResult<Dictionary<string, object>?>(new Dictionary<string, object>
        {
            ["task"] = shared["task"] as string ?? "",
            ["skills"] = skills
        });
    }

    public override Task<(string SkillName, string SkillContent)?> Exec(Dictionary<string, object>? prepRes)
    {
        var task = (prepRes?["task"] as string ?? "").ToLower();
        var skills = prepRes?["skills"] as Dictionary<string, string> ?? new Dictionary<string, string>();

        string preferred;
        if (task.Contains("checklist") || task.Contains("steps"))
        {
            preferred = "checklist_writer";
        }
        else
        {
            preferred = "executive_brief";
        }

        if (skills.TryGetValue(preferred, out var content))
        {
            return Task.FromResult<(string, string)?>((preferred, content));
        }

        var first = skills.First();
        return Task.FromResult<(string, string)?>((first.Key, first.Value));
    }

    public override Task<string?> Post(TShared shared, Dictionary<string, object>? prepRes, (string SkillName, string SkillContent)? execRes)
    {
        shared["selected_skill"] = execRes?.SkillName ?? "";
        shared["selected_skill_content"] = execRes?.SkillContent ?? "";
        return Task.FromResult<string?>("default");
    }

    private static Dictionary<string, string> LoadSkills()
    {
        return new Dictionary<string, string>
        {
            ["checklist_writer"] = """
# Checklist Writer Skill

Convert requests into clear, actionable checklists.

## Rules
- Use numbered steps.
- Keep each step short and verifiable.
- Highlight dependencies and blockers.
- End with a "Definition of Done" section.
""",
            ["executive_brief"] = """
# Executive Brief Skill

You are writing for senior leaders.

## Rules
- Keep it concise and decision-oriented.
- Start with 3 bullet point summary.
- Include risks and recommended next action.
- Avoid implementation-level details unless critical.
"""
        };
    }
}

class ApplySkill : Node<TShared, Dictionary<string, object>, string>
{
    public override Task<Dictionary<string, object>?> Prep(TShared shared)
    {
        return Task.FromResult<Dictionary<string, object>?>(new Dictionary<string, object>
        {
            ["task"] = shared["task"] as string ?? "",
            ["skill_name"] = shared["selected_skill"] as string ?? "",
            ["skill_content"] = shared["selected_skill_content"] as string ?? ""
        });
    }

    public override Task<string?> Exec(Dictionary<string, object>? prepRes)
    {
        var task = prepRes?["task"] as string ?? "";
        var skillName = prepRes?["skill_name"] as string ?? "";
        var skillContent = prepRes?["skill_content"] as string ?? "";

        var prompt = $"""
You are running an Agent Skill.

Skill name: {skillName}

Skill instructions:
---
{skillContent}
---

User task:
{task}

Follow the skill instructions exactly and return the final result only.
""";

        Console.WriteLine($"[ApplySkill] Calling LLM with skill: {skillName}");
        return Task.FromResult<string?>(MockCallLLM(prompt));
    }

    public override Task<string?> Post(TShared shared, Dictionary<string, object>? prepRes, string? execRes)
    {
        shared["result"] = execRes;
        return Task.FromResult<string?>("default");
    }

    private static string MockCallLLM(string prompt)
    {
        if (prompt.Contains("checklist_writer"))
        {
            return """
1. Review the launch plan objectives and key milestones
2. Identify dependencies between teams (engineering, marketing, sales)
3. Define verification criteria for each milestone
4. Highlight blockers and assign owners
5. Establish escalation path for risks

**Definition of Done:**
- All milestones have clear owners
- Dependencies are documented
- Blockers have mitigation plans
""";
        }
        else
        {
            return """
**Executive Summary:**
- Launch plan reviewed and aligned with Q4 objectives
- Cross-functional dependencies identified and managed
- Key risks flagged with mitigation strategies in place

**Risks:**
- Resource constraints may impact timeline
- External dependencies require monitoring

**Recommended Next Action:**
- Secure additional engineering capacity for Q4
- Schedule weekly risk review with stakeholders
""";
        }
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        var task = "Summarize this launch plan for a VP audience";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--") && i + 1 < args.Length)
            {
                task = args[i + 1];
                break;
            }
        }

        var shared = new Dictionary<string, object>
        {
            ["task"] = task
        };

        Console.WriteLine($"🧩 Task: {task}\n");

        var selectSkill = new SelectSkill();
        var applySkill = new ApplySkill();

        selectSkill.On("default").To(applySkill);

        var flow = new Flow<TShared>(selectSkill);
        await flow.Run(shared);

        Console.WriteLine("\n=== Skill Used ===");
        object? skillUsed;
        shared.TryGetValue("selected_skill", out skillUsed);
        Console.WriteLine(skillUsed?.ToString() ?? "(none)");

        Console.WriteLine("\n=== Output ===");
        object? result;
        shared.TryGetValue("result", out result);
        Console.WriteLine(result?.ToString() ?? "(no result)");
    }
}
