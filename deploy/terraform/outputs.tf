output "resource_group_name" {
  value = azurerm_resource_group.this.name
}

output "container_registry_login_server" {
  description = "Push images here: <this>/tickit-agent-host:<tag>"
  value       = azurerm_container_registry.this.login_server
}

output "container_registry_name" {
  value = azurerm_container_registry.this.name
}

output "container_app_name" {
  value = azurerm_container_app.this.name
}

output "container_app_fqdn" {
  description = "Only resolvable/reachable if ingress_external = true, or from inside the Container Apps environment (e.g. a peered VNet) otherwise."
  value       = try(azurerm_container_app.this.ingress[0].fqdn, null)
}

output "key_vault_name" {
  value = azurerm_key_vault.this.name
}

output "container_app_identity_client_id" {
  description = "The user-assigned managed identity's client ID - useful if you ever need to grant it access to something else (e.g. once a real Team Support API needs its own credential)."
  value       = azurerm_user_assigned_identity.container_app.client_id
}

output "storage_account_name" {
  description = "Holds the audit-log Azure Files share mounted into the container at /data."
  value       = azurerm_storage_account.this.name
}
