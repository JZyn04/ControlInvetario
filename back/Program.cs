using back.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var configuredConnection = builder.Configuration.GetConnectionString("Inventory")
    ?? builder.Configuration["DATABASE_URL"];

if (string.IsNullOrWhiteSpace(configuredConnection))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings__Inventory or DATABASE_URL before starting the application.");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalFrontend", policy =>
        policy.WithOrigins("http://localhost:5070", "https://localhost:7219")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(PostgresConnection.Normalize(configuredConnection)));

var cloudRunPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(cloudRunPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{cloudRunPort}");
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("LocalFrontend");
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapFallbackToFile("index.html");

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await database.Database.EnsureCreatedAsync();
}

await app.RunAsync();
