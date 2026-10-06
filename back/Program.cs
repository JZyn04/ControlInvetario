using back.Data;
using back.Auth;
using back.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var configuredConnection = builder.Configuration.GetConnectionString("Inventory")
    ?? builder.Configuration["DATABASE_URL"];

if (string.IsNullOrWhiteSpace(configuredConnection))
{
    throw new InvalidOperationException(
        "Configure ConnectionStrings__Inventory or DATABASE_URL before starting the application.");
}

builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalFrontend", policy =>
        policy.WithOrigins("http://localhost:5070", "https://localhost:7219")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});
builder.Services.AddDbContext<InventoryDbContext>(options =>
    PostgresConnection.Configure(options, configuredConnection));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUsuario>();
builder.Services.AddScoped<AccesoEmpresa>();
builder.Services.AddScoped<AdministracionEmpresa>();
builder.Services.AddScoped<IAuthorizationHandler, PermisoHandler>();
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddDataProtection().SetApplicationName("ControlInventario")
    .PersistKeysToDbContext<InventoryDbContext>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "Inventario.Csrf";
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Inventario.Sesion";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            var database = context.HttpContext.RequestServices.GetRequiredService<InventoryDbContext>();
            var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(id, out var usuarioId) ||
                !await database.Usuarios.AnyAsync(item => item.Id == usuarioId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();
                return;
            }
            var empresaClaim = context.Principal?.FindFirstValue("EmpresaId");
            if (empresaClaim is not null)
            {
                var acceso = context.HttpContext.RequestServices.GetRequiredService<AccesoEmpresa>();
                if (await acceso.Seleccionada(context.Principal) is null)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                }
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EmpresaSeleccionada", policy => policy.RequireAuthenticatedUser().RequireClaim("EmpresaId"));
    foreach (var clave in Permisos.Catalogo.Select(item => item.Clave).Append(Permisos.AdministrarEmpresa))
        options.AddPolicy(clave, policy => policy.RequireAuthenticatedUser().AddRequirements(new PermisoRequirement(clave)));
});

var cloudRunPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(cloudRunPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{cloudRunPort}");
}

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(cloudRunPort))
{
    var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto };
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("LocalFrontend");
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapFallbackToFile("index.html");

// Las migraciones usan la conexión directa; las consultas normales conservan el pooler.
var migrationOptions = new DbContextOptionsBuilder<InventoryDbContext>();
PostgresConnection.Configure(migrationOptions, configuredConnection, direct: true);
await using (var database = new InventoryDbContext(migrationOptions.Options))
{
    var expectedSchema = new Npgsql.NpgsqlConnectionStringBuilder(PostgresConnection.Direct(configuredConnection)).SearchPath;
    if (!string.IsNullOrEmpty(expectedSchema))
    {
        await database.Database.OpenConnectionAsync();
        await using var schemaCheck = database.Database.GetDbConnection().CreateCommand();
        schemaCheck.CommandText = "SELECT current_schema()";
        if ((string?)await schemaCheck.ExecuteScalarAsync() != expectedSchema)
            throw new InvalidOperationException("La conexión no seleccionó el esquema solicitado; migración cancelada.");
    }
    await database.Database.MigrateAsync();
}

await app.RunAsync();
