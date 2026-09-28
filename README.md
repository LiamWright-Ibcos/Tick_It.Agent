# Tick It - Agentic Resolution: Phase 1 (.NET / C#)

Phase 1 of the AI-Assisted First-Line Support agent for the Ibcos GOLD estate, rebuilt on
.NET 8 / C# as the project's new base technology. This follows on from the Node/TypeScript
PoC (`ibcos-first-line-support-poc.zip`, see `first-line-support-poc-architecture.md` in the
project) and extends it with the autonomy-tier progression logic ("Phase 1" in the
playbook's roadmap) that the PoC deliberately didn't attempt.

**Read this whole file before running anything against a real GOLD tenant.** Several pieces
that look "live" in the code are deliberately never executed in this build - see
[What's real vs. simulated](#whats-real-vs-simulated), the
[provisioning checklist](#provisioning-checklist), and
[Going live on actions](#going-live-on-actions).

## Solution layout

```
TickIt.Agent.sln
src/
  TickIt.Core/              Domain models, ITicketSource/IKnowledgeGrounder/IClassifier/
                             IActionExecutor/IAuditSink interfaces, the autonomy TierPolicy.
  TickIt.TeamSupport.Mcp/    Standalone MCP server for Team Support - a SEPARATE project,
                             same as the original ask for the Node PoC. Five read-only
                             tools, no write tool anywhere. Fixture-backed today.
  TickIt.Atlassian/          Jira/Confluence grounding: a fixture grounder (used now) and a
                             real REST client (written, not exercised - no credential yet).
  TickIt.Agent/              Pipeline orchestration, the simulation/live classifier switch,
                             action proposal, the dry-run action executor, the (gated) live
                             action executor, the audit sink.
  TickIt.Agent.Host/         ASP.NET Core host: spawns TickIt.TeamSupport.Mcp as a real MCP
                             subprocess (stdio), runs the pipeline on a timer, and exposes
                             GET /health, POST /trigger, GET /report.
docs/
  live-action-executor-signoff.md   Sign-off criteria required before any live-action flag
                             below is ever set true outside local testing.
tests/
  TickIt.Tests/              xUnit tests for the tier policy, the simulation classifier, the
                             fixture grounder, action proposal, the dry-run and live action
                             executors, and the MCP tool contracts.
```

This mirrors the PoC's two-project split (`team-support-mcp/` + `first-line-support-agent/`)
but adds the pieces Phase 1 needed: a real tier ladder with Tier 2 auto-action *decisions*,
a proper solution/DI structure, and an HTTP surface instead of a one-shot script.

## Running it

Requires the .NET 8 SDK.

```
dotnet build TickIt.Agent.sln
dotnet test tests/TickIt.Tests/TickIt.Tests.csproj
dotnet run --project src/TickIt.Agent.Host
```

On startup the host spawns `TickIt.TeamSupport.Mcp` as a subprocess over stdio (a genuine
MCP client/server round trip, not an in-process shortcut) and immediately runs one triage
pass against its fixture tickets. It then re-runs on a timer (`Polling:IntervalSeconds` in
`appsettings.json`, default 300s).

- `GET /health` - liveness check.
- `POST /trigger` - run the pipeline once, on demand.
- `GET /report` - the most recent run's results as Markdown (the .NET equivalent of the
  PoC's `output/triage-report.md`).
- `audit-log.jsonl` (working directory) - append-only JSON-lines record of every
  observation, classification, tier decision and action (dry-run or, once ever enabled,
  live).

You can also run the MCP server on its own and talk to it with any MCP client:

```
dotnet run --project src/TickIt.TeamSupport.Mcp
```

## What's real vs. simulated

Same posture as the Node PoC, carried forward deliberately rather than re-litigated:

| Piece | Status in this build |
|---|---|
| Team Support tickets | **Fixture.** `TickIt.TeamSupport.Mcp/Fixtures/tickets.json` - synthetic tickets, no real Team Support sandbox/API exists yet. |
| Jira/Confluence grounding | **Fixture, seeded with real data.** `TickIt.Atlassian/Fixtures/gold-jira-snapshot.json` holds three real GOLD issues (GOLD-16270, GOLD-16269, GOLD-16268) pulled via the Atlassian connector while building this project, scored by simple keyword overlap. Most tickets legitimately won't match anything in it - that's a three-issue snapshot, not a search index. |
| Classification | **Simulation.** `SimulationClassifier` is a transparent rule-based keyword classifier. Every result is stamped `ReasoningMode.Simulation` so nothing downstream can mistake it for a model's judgement. |
| Autonomy tier *decisions* | **Real logic, exercised.** `TierPolicy` genuinely evaluates confidence/category/priority and can decide Tier 0, 1, or 2. This is the Phase 1 addition the PoC didn't have. |
| Actions (notify, tag, link, propose-close) | **Dry-run by default; a gated live path now exists.** `DryRunActionExecutor` stays the effective `IActionExecutor` unless `ActionExecutor:UseLiveExecutor` is deliberately set true - see `LiveActionExecutor` below and [Going live on actions](#going-live-on-actions). |
| Live classifier / live Atlassian client | **Written, not executed.** `LiveClaudeClassifier` and `LiveAtlassianKnowledgeGrounder` are real HTTP clients, unit-testable, but have never run against the real APIs in this sandbox - no credentials exist yet (see below). |
| `LiveActionExecutor` | **Implemented for two of four action types, gated off by default.** `NotifyTeamsChannel` (Tier 1, via a Teams incoming webhook) and `LinkJiraIssue` (Tier 2, via the Jira REST API) will call out for real once `UseLiveExecutor`, the matching per-tier flag, and the relevant credential are all set - see its class remarks. `ApplyCategoryTag` and `ProposeDuplicateClose` still throw unconditionally: no real Team Support write API/credential exists to call at all yet. Not the effective executor unless `UseLiveExecutor` is true, and that shouldn't be set without the sign-off in `docs/live-action-executor-signoff.md`. |

## Provisioning checklist

Unchanged from the PoC (`first-line-support-poc-architecture.md`) - nothing here has been
provisioned since that doc was last updated (2026-09-15):

1. **Anthropic API key** for `LiveClaudeClassifier` - service-account key, not personal.
   Set `Classifier:AnthropicApiKey` (or the `Classifier__AnthropicApiKey` environment
   variable) and the host switches to it automatically - `ClassifierOptions.IsLiveConfigured`
   is checked once at startup, nothing else needs to change.
2. **Atlassian service-account Bearer token** for `LiveAtlassianKnowledgeGrounder` (and now
   also for `LiveActionExecutor`'s `LinkJiraIssue` path) - set `Atlassian:SiteBaseUrl` and
   `Atlassian:BearerToken`. Confirm the current Jira Cloud REST endpoint shape against
   Atlassian's docs first; it has moved before.
3. **Real Team Support API/sandbox credential** - still being chased internally, no
   external admin-console path exists for this one. Once it exists, add a new adapter class
   in `TickIt.TeamSupport.Mcp/Adapters/` implementing `ITicketSource` and change the single
   DI registration in that project's `Program.cs`. Nothing else changes. This is also the
   blocker for `LiveActionExecutor`'s `ApplyCategoryTag`/`ProposeDuplicateClose` paths -
   there is no write API to call yet, credential or not.
4. **Teams incoming webhook** for `LiveActionExecutor`'s `NotifyTeamsChannel` path - an
   admin on the target Teams channel adds a "Workflows"/incoming-webhook connector and the
   URL goes in `TeamsNotification:WebhookUrl`. Nobody has been asked to do this yet.

None of these being provisioned yet is one reason every action still defaults to dry-run -
see `DryRunActionExecutor`'s remarks. The other reason is the separate sign-off gate below,
which stays required even once all credentials land.

## Going live on actions

`LiveActionExecutor` (in `TickIt.Agent/Actions/`) is a real implementation, not a stub, for
`NotifyTeamsChannel` and `LinkJiraIssue` - but it is only ever resolved as the effective
`IActionExecutor` when `ActionExecutor:UseLiveExecutor` is `true` in config (default
`false`), and even then each action type has its own gate:

- `NotifyTeamsChannel` additionally needs `ActionExecutor:EnableLiveNotifications = true`
  and a `TeamsNotification:WebhookUrl` configured.
- `LinkJiraIssue` additionally needs `ActionExecutor:EnableLiveTier2Actions = true` and the
  Atlassian credential from the provisioning checklist above.
- `ApplyCategoryTag` and `ProposeDuplicateClose` throw regardless of any flag - there is no
  real Team Support write API/credential to call yet.
- `SuggestKnownIssueLink` throws - no live path has been designed for it.

None of the `Enable*`/`UseLiveExecutor` flags should be set true outside a developer's own
local testing without following **`docs/live-action-executor-signoff.md`** first - it lays
out separate sign-off criteria for the low-risk Tier 1 notification path and the
higher-risk Tier 2 state-changing path, including the human-override window and kill switch
that don't exist yet and are prerequisites for Tier 2 specifically.

## The autonomy tier ladder (Phase 1 addition)

The PoC only ever reached Tier 0 (Observe) or Tier 1 (Notify-and-suggest) - see
`AutonomyTier` in `TickIt.Core`. This build adds the *decision* logic for Tier 2
(Auto-action-low-risk): `TierPolicy` recognises when a ticket is confidently classified into
a narrow, low-risk category (password reset, duplicate, how-to question, known-issue match)
and isn't P1/P2, and marks it as a Tier 2 candidate. Whether that's ever actually **executed**
is a separate, stricter gate - see [Going live on actions](#going-live-on-actions) above.
Tier 3 (Auto-resolve) is modelled in the enum only, for completeness against the playbook's
full ladder; nothing in this codebase implements or permits it.

Tune the thresholds in `TierPolicyOptions` if the defaults (Tier 1 at 55% confidence, Tier 2
at 85%) don't match what the business actually wants - they're a starting judgement call,
not a value handed down from the playbook, and the sign-off doc calls out that Team Ada
should explicitly own them before they ever drive a live action.

## Deployment

Devops-facing Terraform, a Dockerfile, and a CI/CD workflow for taking this to Azure Container
Apps live in `deploy/` - see `deploy/README.md` for the full walkthrough (architecture,
one-time setup, deploying, supplying credentials, verifying, rollback, and the kill-switch
gap). It implements the Azure option from `phase1-go-live-runbook.md` Phase 3 and defaults to
exactly the same dry-run/simulation posture described above - nothing about deploying this
changes what's real vs. simulated until credentials and the sign-offs in
`docs/live-action-executor-signoff.md` say otherwise.

## Open items carried over from the Draft 2 findings

Worth remembering while working on this, per `ai-first-line-support-playbook-draft2-findings.md`:
the kill-switch design (still entirely conceptual - and now a named prerequisite in
`docs/live-action-executor-signoff.md` before Tier 2 goes live) and the GOLD known-issue
content structure (still undefined, which is why grounding is a three-issue snapshot rather
than a real search). The Support AI Agent alignment question is resolved - see
`phase1-dotnet-build-log.md`: this sits with Team Ada, led by Liam.
