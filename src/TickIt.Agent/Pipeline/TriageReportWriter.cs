using System.Text;
using TickIt.Core;

namespace TickIt.Agent.Pipeline;

/// <summary>Renders a run's results as Markdown - the .NET equivalent of the Node PoC's output/triage-report.md.</summary>
public static class TriageReportWriter
{
    public static string Render(IReadOnlyList<TriageResult> results, DateTimeOffset generatedAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Tick It - Triage Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {generatedAt:u}");
        sb.AppendLine($"Tickets processed: {results.Count}");
        sb.AppendLine();

        if (results.Count == 0)
        {
            sb.AppendLine("_No new tickets to triage._");
            return sb.ToString();
        }

        var reasoningModes = results.Select(r => r.Classification.ReasoningMode).Distinct().ToList();
        sb.AppendLine($"Reasoning mode: {string.Join(", ", reasoningModes)}" +
                       (reasoningModes.Contains(ReasoningMode.Simulation)
                           ? " - **simulation results are not a real model's judgement; treat as a wiring demonstration only.**"
                           : ""));
        sb.AppendLine();

        foreach (var result in results)
        {
            var t = result.Ticket;
            sb.AppendLine($"## {t.Number} - {t.Title}");
            sb.AppendLine();
            sb.AppendLine($"- Priority: {t.Priority} | Product: {t.Product} | Status: {t.Status}");
            sb.AppendLine($"- Category: {result.Classification.Category} (confidence {result.Classification.ConfidenceScore:P0}, {result.Classification.ReasoningMode})");
            sb.AppendLine($"- Autonomy tier: **{result.TierDecision.Tier}** - {result.TierDecision.Reason}");
            sb.AppendLine($"- Summary: {result.Classification.Summary}");
            sb.AppendLine($"- Suggested next step: {result.Classification.SuggestedNextStep}");

            if (result.Classification.MatchedKnowledge.Count > 0)
            {
                sb.AppendLine("- Grounding references:");
                foreach (var k in result.Classification.MatchedKnowledge)
                {
                    sb.AppendLine($"  - [{k.Key}]({k.Url}) - {k.Title} (relevance {k.RelevanceScore:P0})");
                }
            }

            if (result.ProposedActions.Count > 0)
            {
                sb.AppendLine("- Proposed actions (all dry-run in this build):");
                foreach (var (action, exec) in result.ProposedActions.Zip(result.ExecutionResults))
                {
                    sb.AppendLine($"  - [{action.Type}] {action.Description} -> {exec.Mode}: {exec.Outcome}");
                }
            }
            else
            {
                sb.AppendLine("- Proposed actions: none (Observe tier).");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
