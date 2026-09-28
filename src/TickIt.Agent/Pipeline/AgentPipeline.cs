using Microsoft.Extensions.Logging;
using TickIt.Agent.Actions;
using TickIt.Core;

namespace TickIt.Agent.Pipeline;

/// <summary>
/// Wires ITicketSource -> IKnowledgeGrounder -> IClassifier -> TierPolicy -> ActionProposer
/// -> IActionExecutor -> IAuditSink for each ticket. This is the .NET equivalent of the Node
/// PoC's first-line-support-agent pipeline, extended with the Tier 2 decision logic.
/// </summary>
public sealed class AgentPipeline
{
    private readonly ITicketSource _ticketSource;
    private readonly IKnowledgeGrounder _grounder;
    private readonly IClassifier _classifier;
    private readonly TierPolicy _tierPolicy;
    private readonly ActionProposer _actionProposer;
    private readonly IActionExecutor _actionExecutor;
    private readonly IAuditSink _auditSink;
    private readonly ILogger<AgentPipeline> _logger;

    public AgentPipeline(
        ITicketSource ticketSource,
        IKnowledgeGrounder grounder,
        IClassifier classifier,
        TierPolicy tierPolicy,
        ActionProposer actionProposer,
        IActionExecutor actionExecutor,
        IAuditSink auditSink,
        ILogger<AgentPipeline> logger)
    {
        _ticketSource = ticketSource;
        _grounder = grounder;
        _classifier = classifier;
        _tierPolicy = tierPolicy;
        _actionProposer = actionProposer;
        _actionExecutor = actionExecutor;
        _auditSink = auditSink;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TriageResult>> RunAsync(CancellationToken ct = default)
    {
        var tickets = await _ticketSource.ListNewTicketsAsync(ct);
        _logger.LogInformation("Fetched {Count} new ticket(s) to triage.", tickets.Count);

        var results = new List<TriageResult>(tickets.Count);
        foreach (var ticket in tickets)
        {
            results.Add(await ProcessTicketAsync(ticket, ct));
        }

        return results;
    }

    public async Task<TriageResult> ProcessTicketAsync(Ticket ticket, CancellationToken ct = default)
    {
        await _auditSink.RecordAsync(new AuditEntry
        {
            Kind = AuditEntryKind.TicketObserved,
            TicketId = ticket.Id,
            Summary = $"Observed ticket {ticket.Number}: {ticket.Title}"
        }, ct);

        var knowledge = await _grounder.GroundAsync(ticket, ct);
        var classification = await _classifier.ClassifyAsync(ticket, knowledge, ct);

        await _auditSink.RecordAsync(new AuditEntry
        {
            Kind = AuditEntryKind.ClassificationProduced,
            TicketId = ticket.Id,
            Summary = $"Classified as '{classification.Category}' (confidence {classification.ConfidenceScore:P0}, mode {classification.ReasoningMode})"
        }, ct);

        var tierDecision = _tierPolicy.Evaluate(ticket, classification);

        await _auditSink.RecordAsync(new AuditEntry
        {
            Kind = AuditEntryKind.TierDecided,
            TicketId = ticket.Id,
            Summary = $"Tier decision: {tierDecision.Tier} - {tierDecision.Reason}"
        }, ct);

        var proposedActions = _actionProposer.Propose(ticket, classification, tierDecision);
        var executionResults = new List<ActionExecutionResult>(proposedActions.Count);

        foreach (var action in proposedActions)
        {
            await _auditSink.RecordAsync(new AuditEntry
            {
                Kind = AuditEntryKind.ActionProposed,
                TicketId = ticket.Id,
                Summary = action.Description
            }, ct);

            var executed = await _actionExecutor.ExecuteAsync(action, ct);
            executionResults.Add(executed);

            await _auditSink.RecordAsync(new AuditEntry
            {
                Kind = AuditEntryKind.ActionExecuted,
                TicketId = ticket.Id,
                Summary = $"{executed.Mode}: {executed.Outcome}"
            }, ct);
        }

        return new TriageResult
        {
            Ticket = ticket,
            Classification = classification,
            TierDecision = tierDecision,
            ProposedActions = proposedActions,
            ExecutionResults = executionResults
        };
    }
}
