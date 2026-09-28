terraform {
  required_version = ">= 1.7.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.90"
    }
  }

  # Remote state. Using local state is not safe for anything more than a single person's
  # one-off experiment - fill this in with wherever Ibcos's own Terraform state lives (or a
  # newly created storage account dedicated to it) before the first `terraform init`, then
  # never change the container/key without a documented state-migration step.
  #
  # backend "azurerm" {
  #   resource_group_name  = "rg-terraform-state"
  #   storage_account_name = "sttickitagenttfstate"
  #   container_name       = "tfstate"
  #   key                  = "tickit-agent.tfstate"
  # }
}

provider "azurerm" {
  features {}
}
