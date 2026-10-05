using System.Net;
using Genki.Studio;
using Genki.Studio.Components;

var builder = WebApplication.CreateBuilder(args);
var urls = builder.Configuration["urls"] ?? "http://127.0.0.1:5278";
foreach (var url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsLoopback)
        throw new InvalidOperationException("Genki Studio is a local PC tool. Bind it to localhost or a loopback IP address.");
builder.WebHost.UseUrls(urls);
var root = new DirectoryInfo(builder.Environment.ContentRootPath);
while (root.Parent is not null && !Directory.Exists(Path.Combine(root.FullName, "RealJapanese", "Data"))) root = root.Parent;
var configFile = builder.Configuration["GenkiStudio:ConfigFile"] ?? Path.Combine(root.FullName, ".tooling", "genki.local.json");
builder.Services.AddSingleton(new StudioWorkspace(configFile, root.FullName));
builder.Services.AddSingleton<IStudioModelFactory, StudioModelFactory>();
builder.Services.AddSingleton<StudioCoordinator>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<StudioCoordinator>());
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var app = builder.Build();
app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host.Trim('[', ']');
    if (context.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip) ||
        !(host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address)))
    { context.Response.StatusCode = 403; return; }
    if (context.Request.Headers.Origin is { Count: > 0 } origins &&
        (!Uri.TryCreate(origins[0], UriKind.Absolute, out var origin) || origin.Authority != context.Request.Host.Value || origin.Scheme != context.Request.Scheme))
    { context.Response.StatusCode = 403; return; }
    context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'";
    await next();
});
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
