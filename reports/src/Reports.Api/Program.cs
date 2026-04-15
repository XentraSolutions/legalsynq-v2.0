using Reports.Api.Configuration;
using Reports.Api.Endpoints;
using Reports.Api.Middleware;
using Reports.Application;
using Reports.Infrastructure;
using Reports.Worker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ReportsServiceSettings>(
    builder.Configuration.GetSection(ReportsServiceSettings.SectionName));
builder.Services.Configure<MySqlSettings>(
    builder.Configuration.GetSection(MySqlSettings.SectionName));
builder.Services.Configure<AdapterSettings>(
    builder.Configuration.GetSection(AdapterSettings.SectionName));

builder.Services.AddReportsApplication();
builder.Services.AddReportsInfrastructure();

builder.Services.AddHostedService<ReportWorkerService>();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();

app.MapHealthEndpoints();

app.MapGet("/health", () => Results.Redirect("/api/v1/health", permanent: true))
    .ExcludeFromDescription();
app.MapGet("/ready", () => Results.Redirect("/api/v1/ready", permanent: true))
    .ExcludeFromDescription();

app.Run();
