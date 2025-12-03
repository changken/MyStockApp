# Project Context: Heroku Environment (Terraform)

## Project Overview
This project contains Terraform configuration files to automatically provision and manage a development environment on the Heroku platform. It is designed to set up a basic web application infrastructure including a database.

## Tech Stack
*   **Infrastructure as Code:** Terraform
*   **Cloud Provider:** Heroku
*   **Database:** Heroku Postgres

## Architecture & Resources
The `main.tf` file defines the following resources:
1.  **Heroku App:** A container for the application (Region: US).
    *   Configured with `NODE_ENV = "development"`.
    *   Comments suggest potential usage for Blazor/.NET applications.
2.  **Heroku Addon:** `heroku-postgresql:hobby-dev` (Postgres database).
3.  **Heroku Formation:** A "web" dyno configuration using the "Eco" size tier.

## Key Files
*   `main.tf`: The primary configuration file declaring resources (App, Addon, Formation) and outputs (App URL, Database URL).
*   `provider.tf`: Configures the Terraform provider for Heroku (version ~> 5.0).
*   `variable.tf`: Declares input variables required for the configuration.

## Usage

### Prerequisites
*   [Terraform CLI](https://www.terraform.io/downloads) installed.
*   [Heroku Account](https://signup.heroku.com/) and API Key.

### Setup & Deployment

1.  **Initialize Terraform:**
    Downloads necessary providers.
    ```bash
    terraform init
    ```

2.  **Plan Deployment:**
    Preview changes. You will be prompted for the `app_name` variable.
    ```bash
    terraform plan
    ```
    *Alternatively, pass the variable inline:*
    ```bash
    terraform plan -var="app_name=my-unique-app-name"
    ```

3.  **Apply Configuration:**
    Create the resources on Heroku.
    ```bash
    terraform apply
    ```

### Outputs
After a successful apply, Terraform will output:
*   `app_url`: The public URL of the Heroku app.
*   `postgres_url`: The connection string for the database (marked as sensitive).
