using Reports.Api.Configuration;
using Reports.Api.Endpoints;
using Reports.Api.Middleware;
using Reports.Infrastructure;
using Reports.Worker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ReportsServiceSettings>(
    builder.Configuration.GetSection(ReportsServiceSettings.SectionName));
builder.Services.Configure<MySqlSettings>(
    builder.Configuration.GetSection(MySqlSettings.SectionName));
builder.Services.Configure<AdapterSettings>(
    builder.Configuration.GetSection(AdapterSettings.SectionName));

builder.Services.AddReportsInfrastructure();

builder.Services.AddHostedService<ReportWorkerService>();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();

app.MapHealthEndpoints();

app.Run();
