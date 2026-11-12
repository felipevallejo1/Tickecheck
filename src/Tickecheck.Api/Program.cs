using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Tickecheck.Api.Auth;
using Tickecheck.Api.ErrorHandling;
using Tickecheck.Api.Hubs;
using Tickecheck.Application;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Auth;
using Tickecheck.Infrastructure;
using Tickecheck.Infrastructure.Auth;
using Tickecheck.Infrastructure.Persistence;
using Tickecheck.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// Structured logging: sinks and levels are read from the "Serilog" configuration section.
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services
    .AddControllers()
    // Enums travel as text ("InProgress") instead of numbers: the JSON is readable
    // and does not break if the order of the enum values changes.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HttpTenantProvider>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSignalR();
builder.Services.AddScoped<ITicketNotifier, SignalRTicketNotifier>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (jwtOptions.SigningKey.Length < 32)
{
    // Fail at startup instead of running with a weak or missing key.
    throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 characters.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the claim names exactly as they are in the token ("sub", "role"...),
        // instead of the long XML-style names ASP.NET Core maps them to by default.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            NameClaimType = AppClaims.Name,
            RoleClaimType = AppClaims.Role,
            // The default tolerance is 5 minutes, too much for a 15-minute token.
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            // Browsers cannot send an Authorization header when opening a WebSocket,
            // so SignalR sends the token in the query string. It is accepted from
            // there only for the hub, never for the regular API endpoints.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments(NotificationsHub.Path))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options => options.AddAppPolicies());

var app = builder.Build();

app.UseExceptionHandler();
// Gives a ProblemDetails body to responses that have none (401, 403, 404 from the framework).
app.UseStatusCodePages();

app.UseSerilogRequestLogging(options =>
{
    // Adds who made the request to the single log line written per request.
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId", httpContext.User.FindFirst(AppClaims.UserId)?.Value ?? "anonymous");
        diagnosticContext.Set("TenantId", httpContext.User.FindFirst(AppClaims.TenantId)?.Value ?? "none");
    };
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// The API also serves the Blazor WebAssembly frontend: its static files (the compiled
// app and its assets) are returned from here, so there is one site and one origin.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationsHub>(NotificationsHub.Path);

// Any other URL (like /tickets/123) is a page of the frontend: return its index.html
// and let the Blazor router show the right page.
app.MapFallbackToFile("index.html");

await PrepareDatabaseAsync(app);

app.Run();

// Applying migrations at startup keeps the demo simple (one command to run everything).
// With several instances of the API it would be a separate deployment step instead.
static async Task PrepareDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    if (app.Configuration.GetValue<bool>("Database:SeedDemoData"))
    {
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
    }
}

// Makes the Program class visible to the integration tests (WebApplicationFactory<Program>).
public partial class Program;
