using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP/HTTPS
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8443); // HTTP debug port
    // options.ListenAnyIP(443, listenOptions => listenOptions.UseHttps());
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    string path = context.Request.Path.Value ?? "";

    // Dynamic ResourcePath_ANDROID.dat response
    if (path.EndsWith("ResourcePath_ANDROID.dat", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.ContentType = "application/json";
        var versionData = new[] { new { ExeVer = "2.6", DataVer = "2.4.72" } };
        await context.Response.WriteAsync(JsonSerializer.Serialize(versionData));
        return;
    }

    await next();
});

// Serve static files from cdn/mirror
string mirrorPath = Path.Combine(Directory.GetCurrentDirectory(), "cdn", "mirror");
if (Directory.Exists(mirrorPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(mirrorPath),
        RequestPath = ""
    });
}

app.MapGet("/", () => "TSM CDN Server (.NET 9 Kestrel) is Online.");

Console.WriteLine("[CDN Server] ASP.NET Core Kestrel CDN Server running on port 8443 (HTTP) / 443 (HTTPS)");
app.Run();
