# TickIt.Agent — Azure infrastructure
#
# Implements the "Azure App Service / Container App" path from phase1-go-live-runbook.md
# Phase 3, using Azure Container Apps specifically: a single always-on container running
# TickIt.Agent.Host (which spawns TickIt.TeamSupport.Mcp itself, see deploy/Dockerfile),
# secrets sourced from Key Vault via a user-assigned managed identity (no credentials in
# config files or container env literals), and an Azure Files mount so audit-log.jsonl
# survives a restart/redeploy instead of living only in ephemeral container storage.
#
# Deliberately NOT auto-scaled: TriagePollingWorker has no distributed lock, so a second
# replica would double-run the pipeline against the same tickets and, once live actions are
# ever enabled, could double-fire a Teams notification or a Jira write. min/max replicas are
# both pinned to 1 below - do not change that without building a lock/leader-election first.

data "azurerm_client_config" "current" {}

locals {
  name = lower("${var.name_prefix}-${var.environment}")

  # Container-app secret name -> the environment variable TickIt.Agent actually reads for it
  # (see appsettings.json / ClassifierOptions / AtlassianOptions / TeamsNotificationOptions).
  secret_env_map = {
    "anthropic-api-key"       = "Classifier__AnthropicApiKey"
    "atlassian-site-base-url" = "Atlassian__SiteBaseUrl"
    "atlassian-bearer-token"  = "Atlassian__BearerToken"
    "teams-webhook-url"       = "TeamsNotification__WebhookUrl"
  }
}

resource "azurerm_resource_group" "this" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = var.tags
}

resource "azurerm_log_analytics_workspace" "this" {
  name                = "law-${local.name}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_container_registry" "this" {
  name                = lower(substr(replace("acr${local.name}", "-", ""), 0, 50))
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  sku                 = "Basic"
  admin_enabled       = false
  tags                = var.tags
}

resource "azurerm_user_assigned_identity" "container_app" {
  name                = "id-${local.name}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  tags                = var.tags
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = azurerm_container_registry.this.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.container_app.principal_id
}

# --- Key Vault -----------------------------------------------------------------------------
# RBAC-authorised (not access-policy-based) so the Container App identity's permission is a
# normal role assignment, consistent with the ACR/Storage grants below.

resource "azurerm_key_vault" "this" {
  name                      = "kv-${substr(replace(local.name, "-", ""), 0, 20)}"
  location                  = azurerm_resource_group.this.location
  resource_group_name       = azurerm_resource_group.this.name
  tenant_id                 = data.azurerm_client_config.current.tenant_id
  sku_name                  = "standard"
  purge_protection_enabled  = true
  enable_rbac_authorization = true
  tags                      = var.tags
}

resource "azurerm_role_assignment" "kv_secrets_user_container_app" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.container_app.principal_id
}

# Whoever/whatever runs `terraform apply` (a person or the CI/CD service principal) needs
# write access to actually create/update the secret values below.
resource "azurerm_role_assignment" "kv_secrets_officer_deployer" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_key_vault_secret" "credentials" {
  # for_each can't iterate the sensitive map itself (Terraform refuses a sensitive value as a
  # for_each key) - iterate its (non-sensitive) key names instead and look the value up per
  # instance, which keeps the actual secret values sensitive everywhere else.
  for_each = nonsensitive(toset(keys(var.secrets)))

  name         = each.value
  value        = var.secrets[each.value]
  key_vault_id = azurerm_key_vault.this.id

  depends_on = [azurerm_role_assignment.kv_secrets_officer_deployer]
}

# --- Durable audit log storage --------------------------------------------------------------
# FileAuditSink (TickIt.Agent/Audit/FileAuditSink.cs) appends to a plain file - Container
# Apps' own writable layer is ephemeral, so without this every redeploy/restart would silently
# lose the audit trail the whole tiered-autonomy design depends on for accountability.

resource "azurerm_storage_account" "this" {
  name                     = lower(substr(replace("st${local.name}", "-", ""), 0, 24))
  resource_group_name      = azurerm_resource_group.this.name
  location                 = azurerm_resource_group.this.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  tags                     = var.tags
}

resource "azurerm_storage_share" "audit_log" {
  name                 = "audit-log"
  storage_account_name = azurerm_storage_account.this.name
  quota                = 5
}

resource "azurerm_container_app_environment" "this" {
  name                       = "cae-${local.name}"
  location                   = azurerm_resource_group.this.location
  resource_group_name        = azurerm_resource_group.this.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id
  tags                       = var.tags
}

resource "azurerm_container_app_environment_storage" "audit_log" {
  name                         = "audit-log-storage"
  container_app_environment_id = azurerm_container_app_environment.this.id
  account_name                 = azurerm_storage_account.this.name
  share_name                   = azurerm_storage_share.audit_log.name
  access_key                   = azurerm_storage_account.this.primary_access_key
  access_mode                  = "ReadWrite"
}

# --- The app itself --------------------------------------------------------------------------

resource "azurerm_container_app" "this" {
  name                         = "ca-${local.name}"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = azurerm_resource_group.this.name
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.container_app.id]
  }

  registry {
    server   = azurerm_container_registry.this.login_server
    identity = azurerm_user_assigned_identity.container_app.id
  }

  dynamic "secret" {
    for_each = nonsensitive(toset(keys(var.secrets)))
    content {
      name                = secret.value
      identity            = azurerm_user_assigned_identity.container_app.id
      key_vault_secret_id = azurerm_key_vault_secret.credentials[secret.value].versionless_id
    }
  }

  template {
    min_replicas = 1
    max_replicas = 1

    volume {
      name         = "audit-log"
      storage_type = "AzureFile"
      storage_name = azurerm_container_app_environment_storage.audit_log.name
    }

    container {
      name   = "tickit-agent-host"
      image  = "${azurerm_container_registry.this.login_server}/tickit-agent-host:${var.image_tag}"
      cpu    = var.container_cpu
      memory = var.container_memory

      volume_mounts {
        name = "audit-log"
        path = "/data"
      }

      liveness_probe {
        transport        = "HTTP"
        path             = "/health"
        port             = 8080
        initial_delay    = 15
        interval_seconds = 30
      }

      readiness_probe {
        transport = "HTTP"
        path      = "/health"
        port      = 8080
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Staging"
      }

      env {
        name  = "Polling__IntervalSeconds"
        value = tostring(var.polling_interval_seconds)
      }

      # These three deliberately default to "false" - see docs/live-action-executor-signoff.md
      # before ever setting the corresponding tfvars to true for a real environment.
      env {
        name  = "ActionExecutor__UseLiveExecutor"
        value = tostring(var.use_live_executor)
      }
      env {
        name  = "ActionExecutor__EnableLiveNotifications"
        value = tostring(var.enable_live_notifications)
      }
      env {
        name  = "ActionExecutor__EnableLiveTier2Actions"
        value = tostring(var.enable_live_tier2_actions)
      }

      # Only ever emits an env var for a credential that was actually supplied in var.secrets -
      # an omitted credential means no env var at all, which TickIt.Agent's *Options.IsLiveConfigured
      # checks already treat the same as a blank one.
      dynamic "env" {
        for_each = nonsensitive(toset(keys(var.secrets)))
        content {
          name        = local.secret_env_map[env.value]
          secret_name = env.value
        }
      }
    }
  }

  ingress {
    external_enabled = var.ingress_external
    target_port      = 8080
    transport        = "http"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }
}
