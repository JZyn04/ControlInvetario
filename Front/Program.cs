using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var sharedWebRoot = Path.GetFullPath(
    Path.Combine(app.Environment.ContentRootPath, "..", "back", "wwwroot"));

if (!Directory.Exists(sharedWebRoot))
{
    throw new DirectoryNotFoundException($"No se encontró el frontend en {sharedWebRoot}.");
}

var files = new PhysicalFileProvider(sharedWebRoot);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
app.MapFallback(async context =>
    await context.Response.SendFileAsync(Path.Combine(sharedWebRoot, "index.html")));

app.Run();
