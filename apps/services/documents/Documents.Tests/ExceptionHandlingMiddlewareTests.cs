using System.Text.Json;
using Documents.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Documents.Tests;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_MapsKestrelRequestBodyLimitToPayloadTooLarge()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new BadHttpRequestException(
                "Request body too large.",
                StatusCodes.Status413PayloadTooLarge),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = "corr-upload-limit";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("FILE_TOO_LARGE", body.RootElement.GetProperty("error").GetString());
        Assert.Equal(
            "The request body exceeds the configured maximum upload size.",
            body.RootElement.GetProperty("message").GetString());
        Assert.Equal(
            "corr-upload-limit",
            body.RootElement.GetProperty("correlationId").GetString());
    }
}
