using System.Net;
using System.Text.Json;
using Liens.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Liens.Api.Tests.Tests;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task Unexpected_error_returns_safe_trace_identifier()
    {
        const string traceId = "case-draft-trace-id";
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new Exception("database details must not be returned"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext
        {
            TraceIdentifier = traceId,
        };
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        var error = json.RootElement.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("server_error");
        error.GetProperty("traceId").GetString().Should().Be(traceId);
        json.RootElement.ToString().Should().NotContain("database details");
    }
}
