using System.Text.Json.Serialization;
using Fatoura.Api.Auth;
using Fatoura.Api.Contacts;
using Fatoura.Api.Data;
using Fatoura.Api.Data.Entities;
using Fatoura.Api.Documents;
using Fatoura.Api.Infrastructure;
using Fatoura.Api.Inventory;
using Fatoura.Api.Items;
using Fatoura.Api.Settings;
using Fatoura.Api.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;

// The build-time OpenAPI generator (GetDocument.Insider) starts the app without a database or secrets.
var generatingOpenApi = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (generatingOpenApi)
{
    config.AddInMemoryCollection([new("Jwt:SigningKey", "openapi-generation-placeholder-signing-key")]);
}

services.AddSingleton(TimeProvider.System);
services.AddSingleton<BusinessClock>();
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUser, HttpCurrentUser>();

// Persistence
services.AddScoped<AuditInterceptor>();
services.AddDbContext<FatouraDbContext>((sp, options) =>
{
    var connectionString = config.GetConnectionString("Fatoura")
        ?? throw new InvalidOperationException("Connection string 'Fatoura' is not configured.");
    options.UseSqlServer(connectionString, sql =>
    {
        sql.UseCompatibilityLevel(160);
        sql.EnableRetryOnFailure();
    });
    options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
});

// Identity (users, password hashing, lockout) without cookie authentication.
services.AddIdentityCore<AppUser>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireUppercase = true;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<FatouraDbContext>()
    .AddDefaultTokenProviders();

// JWT bearer authentication
services.AddOptions<JwtOptions>().Bind(config.GetSection(JwtOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
services.AddOptions<SeedOptions>().Bind(config.GetSection(SeedOptions.Section));
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((o, jwt) =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = TokenService.SigningKey(jwt.Value),
            NameClaimType = ClaimNames.Name,
            RoleClaimType = ClaimNames.Role,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Admin, p => p.RequireRole(Roles.Admin))
    .AddPolicy(Policies.Staff, p => p.RequireRole(Roles.Admin, Roles.Cashier))
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

services.AddScoped<TokenService>();
services.AddScoped<SettingsService>();
services.AddScoped<StockService>();
services.AddScoped<SequenceService>();
services.AddScoped<InvoiceService>();

// HTTP API
services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
services.AddValidation();
services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx => ProblemKeys.CamelCase(ctx.ProblemDetails));
services.AddExceptionHandler<ProblemExceptionHandler>();
services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "Fatoura API";
    doc.Info.Description = "UAE VAT sales, purchases and invoicing.";
    return Task.CompletedTask;
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi().AllowAnonymous();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference().AllowAnonymous();
}

var api = app.MapGroup("/api");
api.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok"))).AllowAnonymous().WithTags("Health");
api.MapAuthEndpoints();
api.MapUserEndpoints();
api.MapSettingsEndpoints();
api.MapClientEndpoints();
api.MapSupplierEndpoints();
api.MapItemEndpoints();
api.MapQuotationEndpoints();
api.MapInvoiceEndpoints();
api.MapCreditNoteEndpoints();

if (config.GetValue("Database:Initialize", true) && !generatingOpenApi)
{
    await DbInitializer.InitializeAsync(app.Services, migrate: config.GetValue("Database:Migrate", true));
}

app.Run();

public sealed record HealthResponse(string Status);

public partial class Program;
