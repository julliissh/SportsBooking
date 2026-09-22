using System.Diagnostics;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddOcelot();

var monolithHost = builder.Configuration["Gateway:MonolithHost"]
    ?? throw new InvalidOperationException("Gateway:MonolithHost must be configured.");
var monolithPort = builder.Configuration.GetValue<int?>("Gateway:MonolithPort")
    ?? throw new InvalidOperationException("Gateway:MonolithPort must be configured.");

// Ocelot routes remain in ocelot.json; the destination is supplied by configuration
// and can be overridden with Gateway__MonolithHost / Gateway__MonolithPort.
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Routes:0:DownstreamHostAndPorts:0:Host"] = monolithHost,
    ["Routes:0:DownstreamHostAndPorts:0:Port"] = monolithPort.ToString(),
    ["Routes:1:DownstreamHostAndPorts:0:Host"] = monolithHost,
    ["Routes:1:DownstreamHostAndPorts:0:Port"] = monolithPort.ToString(),
    ["Routes:2:DownstreamHostAndPorts:0:Host"] = monolithHost,
    ["Routes:2:DownstreamHostAndPorts:0:Port"] = monolithPort.ToString()
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseCors();
app.Map("/health", health => health.Run(context =>
    context.Response.WriteAsJsonAsync(new { status = "OK" })));

app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("X-Correlation-Id"))
        context.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();

    var correlationId = context.Request.Headers["X-Correlation-Id"].ToString();
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    var watch = Stopwatch.StartNew();
    await next();

    if (context.Request.Path.StartsWithSegments("/api"))
        app.Logger.LogInformation("{Method} {Path} -> {Target}; {StatusCode}; {Duration} ms [CorrelationId: {CorrelationId}]",
            context.Request.Method, context.Request.Path,
            $"http://{monolithHost}:{monolithPort}", context.Response.StatusCode, watch.ElapsedMilliseconds, correlationId);
});

await app.UseOcelot();
await app.RunAsync();
