using System.Diagnostics;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddOcelot();

var monolithHost = builder.Configuration["Gateway:MonolithHost"] ?? "localhost";
var monolithPort = builder.Configuration.GetValue<int>("Gateway:MonolithPort", 5100);
var authHost     = builder.Configuration["Gateway:AuthHost"]     ?? "localhost";
var authPort     = builder.Configuration.GetValue<int>("Gateway:AuthPort", 5001);
var catalogHost  = builder.Configuration["Gateway:CatalogHost"]  ?? "localhost";
var catalogPort  = builder.Configuration.GetValue<int>("Gateway:CatalogPort", 5002);
var bookingHost  = builder.Configuration["Gateway:BookingHost"]  ?? "localhost";
var bookingPort  = builder.Configuration.GetValue<int>("Gateway:BookingPort", 5003);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    // Routes[0] = /api/v1/auth/** -> AuthService
    ["Routes:0:DownstreamHostAndPorts:0:Host"] = authHost,
    ["Routes:0:DownstreamHostAndPorts:0:Port"] = authPort.ToString(),
    // Routes[1] = /api/v1/users/** -> AuthService
    ["Routes:1:DownstreamHostAndPorts:0:Host"] = authHost,
    ["Routes:1:DownstreamHostAndPorts:0:Port"] = authPort.ToString(),
    // Routes[2] = /api/v1/catalog/** -> CatalogService
    ["Routes:2:DownstreamHostAndPorts:0:Host"] = catalogHost,
    ["Routes:2:DownstreamHostAndPorts:0:Port"] = catalogPort.ToString(),
    // Routes[3] = /api/bookings -> BookingService
    ["Routes:3:DownstreamHostAndPorts:0:Host"] = bookingHost,
    ["Routes:3:DownstreamHostAndPorts:0:Port"] = bookingPort.ToString(),
    // Routes[4] = /api/bookings/** -> BookingService
    ["Routes:4:DownstreamHostAndPorts:0:Host"] = bookingHost,
    ["Routes:4:DownstreamHostAndPorts:0:Port"] = bookingPort.ToString(),
    // Routes[5] = /api/** catch-all -> Monolith
    ["Routes:5:DownstreamHostAndPorts:0:Host"] = monolithHost,
    ["Routes:5:DownstreamHostAndPorts:0:Port"] = monolithPort.ToString(),
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseCors();

app.Map("/health", health => health.Run(ctx =>
    ctx.Response.WriteAsJsonAsync(new { status = "OK", service = "ApiGateway" })));

app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("X-Correlation-Id"))
        context.Request.Headers["X-Correlation-Id"] = Guid.NewGuid().ToString();

    var correlationId = context.Request.Headers["X-Correlation-Id"].ToString();
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    var watch = Stopwatch.StartNew();
    await next();

    if (context.Request.Path.StartsWithSegments("/api"))
    {
        var path = context.Request.Path.Value ?? "";
        var target = path.StartsWith("/api/v1/auth") || path.StartsWith("/api/v1/users")
            ? $"http://{authHost}:{authPort}"
            : path.StartsWith("/api/v1/catalog")
                ? $"http://{catalogHost}:{catalogPort}"
                : path.StartsWith("/api/bookings")
                    ? $"http://{bookingHost}:{bookingPort}"
                    : $"http://{monolithHost}:{monolithPort}";

        app.Logger.LogInformation("{Method} {Path} -> {Target}; {Status}; {Ms}ms [cid:{CId}]",
            context.Request.Method, path, target,
            context.Response.StatusCode, watch.ElapsedMilliseconds, correlationId);
    }
});

await app.UseOcelot();
await app.RunAsync();
