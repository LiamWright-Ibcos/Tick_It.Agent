using TickIt.Core;

namespace TickIt.Agent.Classification;

/// <summary>
/// Transparent rule-based stand-in for a real model call, purely so the pipeline and tier
/// logic can be built and demonstrated without an Anthropic API key - same rationale and
/// same label as the Node PoC's simulation mode. Every result it produces is stamped
/// <see cref="ReasoningMode.Simulation"/> so nothing downstream can mistake it for a real
/// classification.
/// </summary>
public sealed class SimulationClassifier : IClassifier
{
    private static readonly (string Category, string[] Keywords, string NextStep)[] Rules =
    {
        ("password-reset",
            new[] { "password", "reset", "login", "log in", "locked out" },
            "Confirm the identity-provider reset flow and check for a stuck MFA/session token; escalate to the identity team if the reset loop persists."),

        ("how-to-question",
            new[] { "how do i", "how to", "can you point me", "where do i", "how can i" },
            "Point the requester to the relevant GOLD how-to guide/known-issue article; no defect indicated."),

        ("billing-discrepancy",
            new[] { "invoice", "total", "discrepancy", "doesn't match", "does not match", "overcharge" },
            "Compare the job card / batch detail lines against the generated invoice to find where the totals diverge before responding to the customer."),

        ("known-issue-match",
            new[] { "period locked", "gl period", "locked" },
            "Check whether this matches a known GL period-lock defect; if so link the known issue and apply the documented workaround."),
    };

    public Task<ClassificationResult> ClassifyAsync(
        Ticket ticket,
        IReadOnlyList<KnowledgeReference> knowledge,
        CancellationToken ct = default)
    {
        var haystack = $"{ticket.Title} {ticket.Description}".ToLowerInvariant();

        // Pick the rule with the most keyword hits, not just the first one that matches at
        // all - a ticket can plausibly trip more than one rule (e.g. "invoice" plus "period
        // locked"), and the more specific/better-evidenced match should win.
        var matchedRule = Rules
            .Select(r => (Rule: r, Hits: r.Keywords.Count(k => haystack.Contains(k))))
            .Where(x => x.Hits > 0)
            .OrderByDescending(x => x.Hits)
            .Select(x => x.Rule)
            .FirstOrDefault();
        var strongKnowledgeMatch = knowledge.Any(k => k.RelevanceScore >= 0.5);

        string category;
        string nextStep;
        double confidence;
        var matchedKeywords = Array.Empty<string>();

        if (matchedRule != default)
        {
            category = matchedRule.Category;
            nextStep = matchedRule.NextStep;
            matchedKeywords = matchedRule.Keywords.Where(k => haystack.Contains(k)).ToArray();
            confidence = 0.7 + (matchedKeywords.Length > 1 ? 0.15 : 0.0);
        }
        else if (haystack.Contains("duplicate") || haystack.Contains("same issue") || haystack.Contains("same as"))
        {
            category = "duplicate";
            nextStep = "Confirm against the referenced prior ticket/known issue before treating this as a duplicate; link rather than auto-close.";
            confidence = 0.65;
        }
        else if (strongKnowledgeMatch)
        {
            category = "known-issue-match";
            nextStep = "Review the linked knowledge reference for a matching root cause before responding.";
            confidence = 0.6;
        }
        else
        {
            category = "needs-triage";
            nextStep = "No confident automatic classification; route to a human for first-line triage.";
            confidence = 0.35;
        }

        // High-priority tickets get a small confidence haircut, biasing toward human review
        // rather than autonomous action when the stakes are highest - a deliberate policy
        // choice, not a modelling accident.
        if (ticket.Priority is TicketPriority.P1)
        {
            confidence = Math.Max(0.0, confidence - 0.15);
        }

        var rationale = matchedKeywords.Length > 0
            ? $"Simulation classifier matched keyword(s): {string.Join(", ", matchedKeywords)}."
            : strongKnowledgeMatch
                ? "Simulation classifier matched via knowledge grounding relevance."
                : "Simulation classifier found no rule or grounding match.";

        var result = new ClassificationResult
        {
            TicketId = ticket.Id,
            Category = category,
            Summary = Summarize(ticket),
            SuggestedNextStep = nextStep,
            ConfidenceScore = Math.Round(confidence, 2),
            MatchedKnowledge = knowledge,
            ReasoningMode = ReasoningMode.Simulation,
            Rationale = rationale
        };

        return Task.FromResult(result);
    }

    private static string Summarize(Ticket ticket)
    {
        var description = ticket.Description.Length > 160
            ? ticket.Description[..160] + "..."
            : ticket.Description;
        return $"{ticket.Title} - {description}";
    }
}
