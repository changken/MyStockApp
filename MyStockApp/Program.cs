using Microsoft.EntityFrameworkCore;
using MyStockApp.Components;
using MyStockApp.Data;
using MyStockApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 讀取 DATABASE_URL 並轉換
string? BuildNpgsqlConnectionStringFromEnv()
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(databaseUrl) || string.IsNullOrEmpty(databaseUrl))
        return builder.Configuration.GetConnectionString("DefaultConnection"); // 本機

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var host = uri.Host;
    var port = uri.Port;
    var db = uri.AbsolutePath.TrimStart('/');

    // Heroku 需要 SSL
    return $"Host={host};Port={port};Database={db};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}

var connectionString = BuildNpgsqlConnectionStringFromEnv();


// 使用 DbContextFactory（建議）：每次操作建立短命 DbContext，適合 Blazor Server
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));

// 若你專案中仍有直接注入 AppDbContext 的舊程式，下面這行會把 factory 產出的實例以 Scoped 提供（可選）
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// Trading Services
builder.Services.AddScoped<ITradingCostService, TradingCostService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddScoped<ITradingService, TradingService>();
builder.Services.AddScoped<ICsvExportService, CsvExportService>();
builder.Services.AddSingleton<IMarketHoursService, MarketHoursService>();

// 如果有 WASM/Auto 模式，視需求再加對應服務
var app = builder.Build();

// 啟動時自動套用遷移（可選，生產需評估）
using (var scope = app.Services.CreateScope())
{
    // var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
