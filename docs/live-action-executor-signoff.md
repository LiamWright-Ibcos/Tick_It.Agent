# Live Action Executor — sign-off criteria

## Purpose

This is the checklist that has to be satisfied — and recorded, in the log at the bottom of
this file — before anyone sets `ActionExecutor:UseLiveExecutor`, `EnableLiveNotifications`,
or `EnableLiveTier2Actions` to `true` outside a developer's own local testing. None of this
is enforced by the code itself: flipping the config is entirely possible today without any
of it being true. This document is the enforcement.

## Why this is separate from credentials

Provisioning a credential (the Anthropic key, the Atlassian service-account token, a Teams
webhook) makes the live path *capable* of being called. It says nothing about whether that
call is a good idea to make automatically in production. That is a separate, deliberate
decision, and it stays required even once every credential above lands — credentials and
sign-off are two independent gates, and both have to be open.

## Two tiers, two very different risk profiles

`NotifyTeamsChannel` (Tier 1) posts a message to a Teams channel. A human still decides what
to do about it. The blast radius of getting this wrong is a wrong or noisy notification.

`LinkJiraIssue`, and — once a real Team Support write API exists — `ApplyCategoryTag` and
`ProposeDuplicateClose` (all Tier 2), change state in an external system with only a
human-override window rather than prior approval. The blast radius of getting one of these
wrong is a real comment on a real Jira issue, or a real ticket tagged or proposed for
closure, with the correction happening after the fact rather than before. Tier 1 and Tier 2
are sign-off separately below, and there is no reason Tier 1 needs to wait for Tier 2 to be
ready.

## Sign-off criteria — Tier 1 (Teams notifications)

1. Team Ada (Liam, or a delegate) has picked which Teams channel receives notifications, and
   someone with admin rights on that channel has added the incoming-webhook connector and
   supplied the URL.
2. A soak period (suggested: one week) running against the fixture ticket source in a
   non-production environment, with `EnableLiveNotifications=true`, reviewed for volume,
   noise, wrong channel, or wrong content.
3. Written confirmation from Team Ada that Tier 1 live notifications are approved for
   production — anywhere durable (email, a Jira comment, a Confluence page) is fine; record
   the fact and a link in the sign-off log below.

## Sign-off criteria — Tier 2 (Jira link; tag/duplicate-close once they exist)

1. All Tier 1 criteria above already met and stable in production.
2. The human-override window actually built and demonstrated working. Today
   `Tier2HumanOverrideWindowMinutes` is a config value that nothing reads — `LiveActionExecutor`
   executes a Tier 2 action immediately once its gates are open. A real implementation (queue
   the action, only execute if not vetoed within the window) needs to exist before this tier
   goes live, not just be configured.
3. A tested, documented rollback/undo path for `LinkJiraIssue` at minimum — doesn't need to
   be automated, but "we will figure it out when it happens" is not sufficient.
4. A working kill switch: an immediate way to stop all live Tier 2 execution without a
   redeploy. Flagged as still conceptual in `phase1-dotnet-build-log.md`, carried over from
   the Draft 2 findings — needs to exist in at least minimal form before this tier goes live.
5. Team Ada sign-off specifically on the confidence/category thresholds in
   `TierPolicyOptions`. The code comments already flag these as this build's own starting
   judgement call, not something handed down from the playbook — someone needs to explicitly
   own that call before it drives real, autonomous actions.
6. For `ApplyCategoryTag`/`ProposeDuplicateClose` specifically: cannot go live at all until
   the Team Support write-API decision (item 3 in `first-line-support-poc-architecture.md`)
   is resolved and a real adapter is built. This is a hard prerequisite, not something this
   sign-off can waive — `LiveActionExecutor` throws for both regardless of any flag until
   then, by design.

## What "signed off" looks like

A dated, named, one-line record that the relevant criteria were checked — added to the log
below so it lives with the code it gates, rather than in an email nobody can find later.

## Sign-off log

| Date | Tier | Signed off by | Notes |
|---|---|---|---|
| — | — | — | Nothing signed off yet. |
