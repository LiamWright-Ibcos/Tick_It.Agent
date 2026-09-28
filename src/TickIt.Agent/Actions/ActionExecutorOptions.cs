namespace TickIt.Agent.Actions;

/// <summary>
/// Config-driven gates for LiveActionExecutor. Deliberately separate from "is a credential
/// configured" (that's ClassifierOptions/AtlassianOptions/TeamsNotificationOptions's job) -
/// these flags are the human sign-off gate on top of credentials, following the same
/// config-flip pattern as the rest of this build (presence of a real value switches Live in;
/// absence keeps the safe default). See docs/live-action-executor-signoff.md before ever
/// setting either Enable* flag true outside local/dev testing.
/// </summary>
public sealed class ActionExecutorOptions
{
    public const string SectionName = "ActionExecutor";

    /// <summary>
    /// Master switch. False (default) means the DI registration in Program.cs resolves
    /// DryRunActionExecutor regardless of anything below or of any credential being present.
    /// </summary>
    public bool UseLiveExecutor { get; set; }

    /// <summary>
    /// Tier 1 (NotifyAndSuggest) actions - posting a Teams notification. Lower risk: no
    /// external state change, a human still has to act on the suggestion. Still requires
    /// UseLiveExecutor = true and a Teams webhook configured.
    /// </summary>
    public bool EnableLiveNotifications { get; set; }

    /// <summary>
    /// Tier 2 (AutoActionLowRisk) actions - linking a Jira issue, applying a category tag,
    /// proposing a duplicate close. Higher risk: these change external state with only a
    /// human-override window rather than prior approval. Requires UseLiveExecutor = true,
    /// the sign-off in docs/live-action-executor-signoff.md, and (today) still throws for
    /// ApplyCategoryTag/ProposeDuplicateClose regardless, because no real Team Support write
    /// API/credential exists yet to call - see first-line-support-poc-architecture.md item 3.
    /// </summary>
    public bool EnableLiveTier2Actions { get; set; }

    /// <summary>
    /// Minutes a Tier 2 action should sit visible-but-unexecuted before it may actually run,
    /// giving a human a window to veto it. Recorded here as the agreed config shape; the
    /// delay/queue mechanism itself is not built yet - see the sign-off doc, "Human override
    /// window". LiveActionExecutor does not currently read this value.
    /// </summary>
    public int Tier2HumanOverrideWindowMinutes { get; set; } = 30;
}
