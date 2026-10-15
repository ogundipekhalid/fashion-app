using System.Reflection;
using Asp.Versioning;
using AspNetCoreRateLimit;
using FashionCommerce.API.Extensions;
using FashionCommerce.API.Hubs;
using FashionCommerce.API.Middlewares;
using FashionCommerce.Application;
using FashionCommerce.Infrastructure;
using FashionCommerce.Infrastructure.Jobs;
using FashionCommerce.Persistence;
using FashionCommerce.Persistence.Seed;
using Hangfire;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog: request/error/performance logging (see LOGGING requirement in spec)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

// API Versioning: all controllers are versioned via {version:apiVersion} in the route (see ApiControllerBase).
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "FashionCommerce API", Version = "v1" });

    // Lets Swagger UI attach a Bearer token to requests for testing protected endpoints.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

builder.Services.AddApplicationServices();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddBackgroundJobs(builder.Configuration);

// Rate Limiting — IP-based, with tighter rules on auth endpoints (brute-force
// defense) and chat message sending (spam defense), general rules everywhere
// else. Rules live in appsettings.json ("IpRateLimiting" section).
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// CSRF/Antiforgery — registered for completeness and for any future
// cookie-authenticated surface (e.g. a server-rendered admin panel). This
// API's actual auth model is Bearer JWT sent via the Authorization header,
// not cookies, so classic CSRF (which exploits ambient cookie auth) doesn't
// apply to the JSON endpoints as they stand today — there's no cookie for a
// malicious site to ride on. If a cookie-based flow is added later, apply
// [ValidateAntiForgeryToken] / IAntiforgery to those specific endpoints.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// ---------------------------------------------------------------------
// NOTE: This is the composition root. All layers are wired up:
// Application (CQRS/MediatR/validators), Persistence (EF Core + Identity),
// Infrastructure (JWT auth, email, current-user, Paystack, Cloudinary),
// Hangfire (scheduled jobs), and now rate limiting + security headers.
//
// Still to come: SignalR chat hub, AI service client, Flutterwave/Stripe.
// ---------------------------------------------------------------------

var app = builder.Build();

app.UseIpRateLimiting();
app.UseSecurityHeaders();
app.UseSerilogRequestLogging();
app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "FashionCommerce API v1"));

    // Local dev convenience only — applies pending migrations and seeds
    // roles/a SuperAdmin account/baseline catalog data. Never runs outside
    // Development; a real deployment applies migrations via a controlled
    // release step instead (see Migrations/README.md).
    await DataSeeder.SeedAsync(app.Services);
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

// Admin/SuperAdmin-only dashboard at /hangfire for observing and manually
// triggering/retrying jobs — useful ops visibility beyond just the logs.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthFilter() }
});

HangfireRegistration.ScheduleRecurringJobs();

app.Run();
