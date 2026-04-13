using Contracts;
using Liens.Api.Endpoints;
using Liens.Api.Middleware;
using Liens.Infrastructure;

const string ServiceName = "liens";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

builder.Services.AddLiensServices(builder.Configuration);

var app = builder.Build();

var env = app.Environment.EnvironmentName;
app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)));

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)));

app.MapLienEndpoints();

app.Run();
