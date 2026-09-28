# Deploying TickIt.Agent

This is the devops-facing package for taking TickIt.Agent from "builds and runs locally" to
"hosted in Azure" - the Azure path chosen from the two options in
`phase1-go-live-runbook.md` Phase 3 (Windows Service vs. Azure App Service/Container App).
Read that file and `docs/live-action-executor-signoff.md` first if you haven't - this README
covers *how* to deploy, not the policy decisions about *when* live actions get turned on.

## What's here

```
deploy/
  Dockerfile              Multi-stage build: publishes TickIt.Agent.Host and
                           TickIt.TeamSupport.Mcp into one runtime image (see the comment at
                           the top of the file for why they can't be separate containers as
                           this codebase stands today).
  terraform/               Azure infrastructure: resource group, Container Registry, a
                           user-assigned managed identity, Key Vault (RBAC-authorised),
                           Log Analytics, a Container Apps environment + the app itself, and
                           an Azure Files share so audit-log.jsonl survives a restart.
.github/workflows/deploy.yml   CI/CD: build+test -> build & push image (via az acr build) ->
                           terraform plan/apply. See the comment block at the top of that
                           file for required secrets and the GitHub-Actions-vs-Azure-Pipelines
                           assumption it makes.
```

## Architecture this provisions

A single Azure Container App (min & max replicas pinned to **1** - see the comment in
`main.tf`; `TriagePollingWorker` has no distributed lock, so a second replica would double-run
the pipeline and, once live actions are ever enabled, could double-fire a notification or a
Jira write) running the bundled image, in a Container Apps Environment, pulling from a
dedicated Container Registry via managed identity (no registry password anywhere), reading
credentials from Key Vault the same way, and persisting `audit-log.jsonl` to a mounted Azure
Files share instead of the container's own ephemeral storage.

Nothing here changes what's real vs. simulated in the app itself (see the table in the main
`README.md`) - a fresh deploy with no `secrets` supplied comes up exactly as it does locally
today: `SimulationClassifier`, the fixture Jira grounder, the fixture/mock Team Support
adapter, and `DryRunActionExecutor`. That's deliberate. Supplying a credential later is a
`terraform apply` with one more key in `secrets`, not a code or image change - see
`variables.tf`'s description of that variable.

## One-time setup

1. **Remote state.** Local Terraform state is not safe once more than one person or a CI
   pipeline touches this. Create a storage account for state (or reuse an existing one) and
   fill in the commented `backend "azurerm" {}` block in `providers.tf`, then run
   `terraform init` (it will prompt to migrate if state already exists locally - don't let
   that happen; configure the backend before the first real apply).
2. **Who deploys.** Either a person running `az login` + `terraform apply` locally, or the
   CI/CD pipeline via a service principal (`AZURE_CREDENTIALS` secret - see
   `.github/workflows/deploy.yml`'s header comment for the exact permissions it needs:
   Contributor plus enough rights to create role assignments, since this config grants the
   app's managed identity `AcrPull` and `Key Vault Secrets User` itself).
3. **Pick an environment name.** `staging` or `prod` (the `environment` variable is
   validated to one of those two - add more in `variables.tf` if a real need for a third
   shows up).

## Deploying

```bash
# From the repo root:
docker build -f deploy/Dockerfile -t <acr-login-server>/tickit-agent-host:<tag> .
az acr login --name <acr-name>
docker push <acr-login-server>/tickit-agent-host:<tag>

cd deploy/terraform
terraform init
terraform plan  -var environment=staging -var image_tag=<tag>
terraform apply -var environment=staging -var image_tag=<tag>
```

`<tag>` should be a git SHA or CI build number - never `latest` for a real deploy, so a
rollback means picking a known-good tag (`terraform apply -var image_tag=<previous-tag>`)
rather than guessing what `latest` currently points at. On a first apply in a given
subscription there's nothing in the Container Registry yet, so build/push the image with
some tag before the first `terraform apply` that references it.

The CI workflow (`.github/workflows/deploy.yml`) automates all of the above: it builds and
tests the solution, builds the image inside ACR via `az acr build` (no local Docker needed in
CI), then runs `terraform plan`/`apply` against a GitHub Environment (`staging` or `prod`) -
add a required reviewer on the `prod` environment in the repo's settings so a real production
deploy needs a human approval, not just a green build.

## Supplying credentials

Nothing is provisioned yet as of the last project update - see the provisioning checklists in
`first-line-support-poc-architecture.md` and `phase1-dotnet-build-log.md`. When one lands:

```bash
cd deploy/terraform
cat > secrets.auto.tfvars <<'EOF'
secrets = {
  "anthropic-api-key" = "sk-ant-..."
}
EOF
terraform apply -var environment=staging -var image_tag=<current-tag>
```

`secrets.auto.tfvars` is already covered by the root `.gitignore` - never commit it. Add keys
one at a time as each credential is actually issued; see `variables.tf`'s description of the
`secrets` variable for the exact key names it recognises. There is deliberately no Terraform
variable yet for the Team Support write-API credential (checklist item 3) - once that exists
it unlocks a new `ITicketSource` adapter class in code, not an infrastructure change.

**Do not** set `use_live_executor`, `enable_live_notifications`, or
`enable_live_tier2_actions` to `true` for `staging` or `prod` without following
`docs/live-action-executor-signoff.md` first and recording the sign-off there - these are
independent of whether a credential exists, by design.

## Verifying a deploy

```bash
terraform output container_app_fqdn   # empty/unreachable unless ingress_external = true
az containerapp show -g $(terraform output -raw resource_group_name) \
  -n $(terraform output -raw container_app_name) --query properties.runningStatus
az containerapp logs show -g $(terraform output -raw resource_group_name) \
  -n $(terraform output -raw container_app_name) --follow
```

`GET /health` and `GET /report` are the same endpoints described in the main `README.md`.
`ingress_external` defaults to `false` because those endpoints (particularly `POST /trigger`,
which runs the real pipeline on demand) have no authentication in front of them today - reach
them from inside the Container Apps environment / a peered VNet, or treat turning on external
ingress as a security decision that needs an auth layer first, not a convenience toggle.

To confirm a credential actually took effect once one is supplied, check `audit-log.jsonl` on
the mounted share (`az storage file download --account-name $(terraform output -raw
storage_account_name) --share-name audit-log --path audit-log.jsonl`) for `ReasoningMode.Live`
entries rather than `Simulation` - the same signal called out in
`phase1-go-live-runbook.md` Phase 4.

## Rollback

Re-apply with the previous known-good `image_tag`:

```bash
terraform apply -var environment=staging -var image_tag=<previous-tag>
```

`revision_mode = "Single"` means each apply replaces the running revision outright rather than
splitting traffic - fine for a single-instance background-worker app like this one where two
revisions running simultaneously would double-process tickets, but it does mean there's no
built-in gradual rollout to fall back on; a bad deploy is fixed by rolling the tag back, not by
shifting traffic weight.

## Kill switch

`phase1-go-live-runbook.md` Phase 5 and `docs/live-action-executor-signoff.md` both call out
that a kill switch for live actions is still "entirely conceptual" - nothing here builds one.
The nearest thing available at the infrastructure layer today is `terraform apply -var
use_live_executor=false` (falls back to `DryRunActionExecutor` on the next revision) or, for an
immediate stop without waiting on a Terraform run, `az containerapp update --min-replicas 0
--max-replicas 0` to stop the container entirely. Neither is the "flip an env var and the
service stops picking up new tickets while staying otherwise up" design the runbook asks for -
that still needs to be designed and built before Tier 2 ever goes live for real.
