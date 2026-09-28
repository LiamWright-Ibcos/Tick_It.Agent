variable "environment" {
  description = "Deployment environment name, e.g. \"staging\" or \"prod\". Used to build resource names and to pick the ASPNETCORE_ENVIRONMENT value."
  type        = string

  validation {
    condition     = contains(["staging", "prod"], var.environment)
    error_message = "environment must be \"staging\" or \"prod\" (add more here if a real need for a third one shows up)."
  }
}

variable "location" {
  description = "Azure region. Defaults to UK South given Ibcos's UK/EU footprint (see first-line-support-poc-architecture.md and the Draft 2 findings on EU/GDPR exposure) - change only with a data-residency reason."
  type        = string
  default     = "uksouth"
}

variable "name_prefix" {
  description = "Short name prefix used to build every resource name."
  type        = string
  default     = "tickit-agent"
}

variable "tags" {
  description = "Tags applied to every resource."
  type        = map(string)
  default = {
    project = "Tick-It-Agentic-Resolution"
    owner   = "Team-Ada"
  }
}

variable "image_tag" {
  description = "Tag of the tickit-agent-host image already pushed to the Container Registry this run will deploy (a git SHA or CI build number - never \"latest\" for a real deploy, so a rollback means picking a known-good tag rather than guessing what \"latest\" currently points at)."
  type        = string
}

variable "container_cpu" {
  description = "vCPU allocated to the single container. Azure Container Apps requires memory to scale with this in fixed combinations (0.5 CPU -> 1Gi, 1.0 CPU -> 2Gi, etc.)."
  type        = number
  default     = 0.5
}

variable "container_memory" {
  description = "Memory allocated to the single container. Must be a valid pairing for container_cpu - see the comment above."
  type        = string
  default     = "1Gi"
}

variable "polling_interval_seconds" {
  description = "Maps to Polling:IntervalSeconds - how often TriagePollingWorker re-runs the pipeline."
  type        = number
  default     = 300
}

variable "ingress_external" {
  description = <<-EOT
    Whether GET /health, POST /trigger and GET /report are reachable from outside the
    Container Apps environment. There is no authentication in front of these endpoints today
    (see TickIt.Agent.Host/Program.cs) - POST /trigger in particular runs the real pipeline
    on demand. Keep this false (reachable only from inside the Container Apps environment /
    a peered VNet) until an auth layer exists in front of it, and treat turning it on as a
    security decision, not a convenience toggle.
  EOT
  type        = bool
  default     = false
}

variable "use_live_executor" {
  description = "Maps to ActionExecutor:UseLiveExecutor. Default false keeps DryRunActionExecutor as the effective executor regardless of any credential being present. Do not set this true for a real environment without following docs/live-action-executor-signoff.md first and recording the sign-off there."
  type        = bool
  default     = false
}

variable "enable_live_notifications" {
  description = "Maps to ActionExecutor:EnableLiveNotifications (Tier 1 Teams posts). Only takes effect if use_live_executor is also true and a teams-webhook-url secret is supplied. See docs/live-action-executor-signoff.md."
  type        = bool
  default     = false
}

variable "enable_live_tier2_actions" {
  description = "Maps to ActionExecutor:EnableLiveTier2Actions (Tier 2 Jira-link actions). Only takes effect if use_live_executor is also true and the Atlassian credential is supplied. Sign-off criteria in docs/live-action-executor-signoff.md are stricter for this tier (human-override window, kill switch, rollback path) and are not all built yet - see that file before ever setting this true outside local testing."
  type        = bool
  default     = false
}

variable "secrets" {
  description = <<-EOT
    Credential values to store in Key Vault and wire into the Container App as secret-backed
    environment variables, keyed exactly as below (any other key is ignored, since there's
    nothing in TickIt.Agent to map it to):

      "anthropic-api-key"        -> Classifier__AnthropicApiKey
      "atlassian-site-base-url"  -> Atlassian__SiteBaseUrl
      "atlassian-bearer-token"   -> Atlassian__BearerToken
      "teams-webhook-url"        -> TeamsNotification__WebhookUrl

    Omit a key entirely until that credential is actually provisioned - see the provisioning
    checklists in first-line-support-poc-architecture.md and phase1-dotnet-build-log.md.
    TickIt.Agent already treats an absent/blank value as "not live" (IsLiveConfigured checks),
    so no code or image change is needed when a credential lands later - just re-apply with
    the extra key present.

    There is deliberately no key here yet for the Team Support write-API credential (item 3
    in the provisioning checklist): as of the last project update it doesn't exist, and when
    it does it unlocks a new ITicketSource/adapter class rather than a Container App secret.

    Supply this via a gitignored *.auto.tfvars file (see terraform.tfvars.example) or as a
    CI/CD secret variable. Never commit real values to the repo or put them in this variable's
    default.
  EOT
  type        = map(string)
  sensitive   = true
  default     = {}

  validation {
    condition = alltrue([
      for k in keys(var.secrets) : contains(
        ["anthropic-api-key", "atlassian-site-base-url", "atlassian-bearer-token", "teams-webhook-url"],
        k
      )
    ])
    error_message = "secrets keys must be one of: anthropic-api-key, atlassian-site-base-url, atlassian-bearer-token, teams-webhook-url."
  }
}
