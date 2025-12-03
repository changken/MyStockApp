# main.tf（重點片段）
resource "heroku_app" "dev_app" {
  name   = var.app_name
  region = "us"
  stack  = "container"

  config_vars = {
    # Heroku 會提供 PORT，ASP.NET 需綁定 0.0.0.0:${PORT}
    ASPNETCORE_URLS = "http://0.0.0.0:${PORT}"
    ASPNETCORE_ENVIRONMENT = "Production"
  }
}

resource "heroku_addon" "postgres" {
  app_id = heroku_app.dev_app.id
  plan   = "heroku-postgresql:hobby-dev"
}

resource "heroku_formation" "web" {
  app_id    = heroku_app.dev_app.id
  type      = "web"
  quantity  = 1
  size      = "Eco" # 或 Standard-1x
  depends_on = [heroku_addon.postgres]
}

output "app_url" {
  value = heroku_app.dev_app.web_url
}

output "postgres_url" {
  value     = heroku_addon.postgres.config_vars["DATABASE_URL"]
  sensitive = true
}
