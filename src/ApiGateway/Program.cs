using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using ApiGateway.Clients;
using ApiGateway.Middleware;
using ApiGateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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

var routeOverrides = new Dictionary<string, string?>();
var configuredRoutes = builder.Configuration.GetSection("Routes").GetChildren().ToList();
for (int i = 0; i < configuredRoutes.Count; i++)
{
    var downstreamPath = configuredRoutes[i]["DownstreamPathTemplate"] ?? string.Empty;
    string host;
    int port;

    if (downstreamPath.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase) ||
        downstreamPath.StartsWith("/api/v1/users", StringComparison.OrdinalIgnoreCase))
    {
        host = authHost;
        port = authPort;
    }
    else if (downstreamPath.StartsWith("/api/v1/catalog", StringComparison.OrdinalIgnoreCase))
    {
        host = catalogHost;
        port = catalogPort;
    }
    else if (downstreamPath.StartsWith("/api/bookings", StringComparison.OrdinalIgnoreCase))
    {
        host = bookingHost;
        port = bookingPort;
    }
    else
    {
        host = monolithHost;
        port = monolithPort;
    }

    routeOverrides[$"Routes:{i}:DownstreamHostAndPorts:0:Host"] = host;
    routeOverrides[$"Routes:{i}:DownstreamHostAndPorts:0:Port"] = port.ToString();
}

if (routeOverrides.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(routeOverrides);
}

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICatalogCacheService, CatalogCacheService>();

var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = jwt["Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("Jwt:Key is not configured. Configure it via user-secrets or environment variables.");
}
var jwtIssuer = jwt["Issuer"] ?? "SportsBookingAuth";
var jwtAudience = jwt["Audience"] ?? "SportsBookingClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/problem+json";
                var problem = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "Bearer token is missing or invalid.",
                    Instance = ctx.Request.Path
                };
                await ctx.Response.WriteAsJsonAsync(problem);
            }
        };
    });

builder.Services.AddAuthorization();

static FixedWindowRateLimiterOptions FixedOpt(int limit) => new()
{
    Window = TimeSpan.FromMinutes(1),
    PermitLimit = limit,
    QueueLimit = 0
};

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var path = ctx.Request.Path;

        if (path.StartsWithSegments("/health"))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
        var curLoginLimit   = config.GetValue<int>("RateLimits:Login", 10);
        var curBookingLimit = config.GetValue<int>("RateLimits:Booking", 10);
        var curGeneralLimit = config.GetValue<int>("RateLimits:General", 100);
        var curAnonLimit    = config.GetValue<int>("RateLimits:Anonymous", 30);

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        var uid = ctx.User.FindFirstValue("sub")
               ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (HttpMethods.IsPost(ctx.Request.Method) && path.StartsWithSegments("/api/v1/auth/login"))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"login:{ip}", _ => FixedOpt(curLoginLimit));
        }

        if (HttpMethods.IsPost(ctx.Request.Method) && path.StartsWithSegments("/api/bookings"))
        {
            var key = uid is not null ? $"book:user:{uid}" : $"book:ip:{ip}";
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => FixedOpt(curBookingLimit));
        }

        if (uid is not null)
        {
            return RateLimitPartition.GetFixedWindowLimiter($"general:user:{uid}", _ => FixedOpt(curGeneralLimit));
        }

        return RateLimitPartition.GetFixedWindowLimiter($"general:ip:{ip}", _ => FixedOpt(curAnonLimit));
    });

    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            ctx.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            ctx.HttpContext.Response.Headers.RetryAfter = "60";
        }

        ctx.HttpContext.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Rate limit exceeded. Please retry later.",
            Instance = ctx.HttpContext.Request.Path
        };
        await ctx.HttpContext.Response.WriteAsJsonAsync(problem, ct);
    };
});

var authUrl    = builder.Configuration["Services:AuthUrl"]    ?? $"http://{authHost}:{authPort}";
var catalogUrl = builder.Configuration["Services:CatalogUrl"] ?? $"http://{catalogHost}:{catalogPort}";
var bookingUrl = builder.Configuration["Services:BookingUrl"] ?? $"http://{bookingHost}:{bookingPort}";

builder.Services.AddHttpClient<IAuthGwClient, AuthGwClient>(client =>
{
    client.BaseAddress = new Uri(authUrl);
    client.Timeout = TimeSpan.FromSeconds(2);
});

builder.Services.AddHttpClient<ICatalogGwClient, CatalogGwClient>(client =>
{
    client.BaseAddress = new Uri(catalogUrl);
    client.Timeout = TimeSpan.FromSeconds(2);
});

builder.Services.AddHttpClient<IBookingGwClient, BookingGwClient>(client =>
{
    client.BaseAddress = new Uri(bookingUrl);
    client.Timeout = TimeSpan.FromSeconds(2);
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:5173", "http://localhost:8080" };

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (builder.Environment.IsDevelopment())
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    }
    else
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    }
}));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ApiGateway API",
        Version = "v1",
        Description = "API Gateway для SportsBooking (ЛР3): композитний екран, кешування публічного каталогу, обмеження частоти запитів та валідація JWT."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    c.OperationFilter<ApiGateway.Filters.VenueDetailsOperationFilter>(); c.DocumentFilter<ApiGateway.Filters.OcelotRoutesDocumentFilter>();
});

builder.Services.AddOcelot(builder.Configuration)
    .AddQualityOfService();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors();
app.UseMiddleware<GatewayProblemDetailsMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ApiGateway v1");
    c.RoutePrefix = "swagger"; c.EnablePersistAuthorization();
});

app.UseRouting();

app.UseAuthentication();

app.Use(async (context, next) =>
{
    context.Request.Headers.Remove("X-User-Id");
    context.Request.Headers.Remove("X-User-Roles");

    if (context.User.Identity?.IsAuthenticated == true)
    {
        var uid = context.User.FindFirstValue("sub")
               ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(uid))
        {
            context.Request.Headers["X-User-Id"] = uid;
        }

        var identity = (ClaimsIdentity)context.User.Identity;
        var roleClaimType = identity.RoleClaimType ?? ClaimTypes.Role;
        var roles = string.Join(",", context.User.FindAll(roleClaimType).Select(c => c.Value));
        if (!string.IsNullOrWhiteSpace(roles))
        {
            context.Request.Headers["X-User-Roles"] = roles;
        }
    }

    if (!context.Request.Headers.TryGetValue("X-Correlation-Id", out var cid) || string.IsNullOrWhiteSpace(cid))
    {
        cid = Guid.NewGuid().ToString();
        context.Request.Headers["X-Correlation-Id"] = cid;
    }
    context.Response.Headers["X-Correlation-Id"] = cid.ToString();

    var watch = Stopwatch.StartNew();
    await next();

    if (context.Request.Path.StartsWithSegments("/api"))
    {
        var path = context.Request.Path.Value ?? "";
        app.Logger.LogInformation("{Method} {Path}; {Status}; {Ms}ms [cid:{CId}]",
            context.Request.Method, path, context.Response.StatusCode, watch.ElapsedMilliseconds, cid);
    }
});

app.UseRateLimiter();

app.UseAuthorization();

app.UseMiddleware<CatalogCacheMiddleware>();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();

    endpoints.MapGet("/health", async (ICatalogGwClient cat, IBookingGwClient bk, IAuthGwClient auth, CancellationToken ct) =>
    {
        var catTask = cat.CheckHealthAsync(ct);
        var bkTask = bk.CheckHealthAsync(ct);
        var authTask = auth.CheckHealthAsync(ct);

        await Task.WhenAll(catTask, bkTask, authTask);

        var catOk = catTask.Result;
        var bkOk = bkTask.Result;
        var authOk = authTask.Result;
        var healthy = catOk && bkOk && authOk;

        return Results.Json(new
        {
            status = healthy ? "OK" : "Degraded",
            service = "ApiGateway",
            catalog = catOk ? "Available" : "Unavailable",
            booking = bkOk ? "Available" : "Unavailable",
            auth = authOk ? "Available" : "Unavailable",
            timestamp = DateTime.UtcNow
        }, statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }).AllowAnonymous();

    endpoints.MapGet("/health/cache-stats", (ICatalogCacheService cacheService) =>
    {
        return Results.Json(new
        {
            hits = cacheService.Hits,
            misses = cacheService.Misses,
            timestamp = DateTime.UtcNow
        });
    }).AllowAnonymous();
});

await app.UseOcelot();

await app.RunAsync();

public partial class Program { }
