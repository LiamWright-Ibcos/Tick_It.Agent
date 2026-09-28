using Microsoft.Extensions.Logging.Abstractions;
using TickIt.Agent.Actions;
using TickIt.Core;

namespace TickIt.Tests;

public class DryRunActionExecutorTests
{
    [Theory]
    [InlineData(ActionType.NotifyTeamsChannel)]
    [InlineData(ActionType.LinkJiraIssue)]
    [InlineData(ActionType.ApplyCategoryTag)]
    [InlineData(ActionType.ProposeDuplicateClose)]
    public async Task NeverActuallyExecutes_RegardlessOfActionType(ActionType type)
    {
        var executor = new DryRunActionExecutor(NullLogger<DryRunActionExecutor>.Instance);
        var action = new ProposedAction
        {
            TicketId = "TS-1",
            Type = type,
            Description = "test action",
            Tier = AutonomyTier.AutoActionLowRisk
        };

        var result = await executor.ExecuteAsync(action);

        Assert.Equal(ActionExecutionMode.DryRun, result.Mode);
        Assert.False(result.Executed);
    }

    // LiveActionExecutor now has real (gated) behaviour instead of unconditionally throwing -
    // see LiveActionExecutorTests.cs for its coverage.
}
