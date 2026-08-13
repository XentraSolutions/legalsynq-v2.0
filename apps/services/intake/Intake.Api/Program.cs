using System.IdentityModel.Tokens.Jwt;
using System.Text;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using Intake.Api.Authorization;
using Intake.Api.Endpoints;
using Intake.Api.Middleware;
using Intake.Application;
using Intake.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

const string ServiceName = "intake";
const string MultiScheme = "MultiAuth";

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", ServiceName)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IIntakeFoundationService, IntakeFoundationService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

if (!builder.Environment.IsDevelopment() &&
    signingKey.StartsWith("REPLACE_VIA_SECRET", StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be supplied through deployment configuration outside Development.");
}

var tokenReader = new JwtSecurityTokenHandler();

builder.Services
    .AddAuthentication(MultiScheme)
    .AddPolicyScheme(MultiScheme, MultiScheme, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var token = tokenReader.ReadJwtToken(authorization["Bearer ".Length..].Trim());
                    if (string.Equals(
                        token.Issuer,
                        ServiceTokenAuthenticationDefaults.DefaultIssuer,
                        StringComparison.Ordinal))
                    {
                        return ServiceTokenAuthenticationDefaults.Scheme;
                    }
                }
                catch
                {
                    // Let the user JWT handler produce the normal auth failure.
                }
            }

            return JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
            {
                KeyId = ServiceTokenAuthenticationDefaults.UserTokenKeyId,
            },
            RoleClaimType = "role",
            NameClaimType = "sub",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    })
    .AddServiceTokenBearer(builder.Configuration, failFastIfMissingSecret: false);

builder.Services.AddAuthorization(IntakeAuthorizationPolicies.AddTo);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCorrelationId();
app.UseMiddleware<IntakeConfigurationExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Synq Intake v1"));
}

app.MapHealthEndpoints();
app.MapInfoEndpoints();
app.MapIntakeConfigurationEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    service = ServiceName,
    status = "running",
    timestamp = DateTimeOffset.UtcNow,
})).AllowAnonymous();

app.MapGet("/tenant-context", (
    ICurrentRequestContext context,
    HttpContext httpContext) => Results.Ok(new
{
    tenantId = context.TenantId,
    orgId = context.OrgId,
    userId = context.UserId,
    correlationId = httpContext.GetCorrelationId(),
}))
.RequireAuthorization()
.WithTags("Foundation");

app.Logger.LogInformation(
    "Starting {Service} in {Environment}",
    ServiceName,
    app.Environment.EnvironmentName);

await app.RunAsync();