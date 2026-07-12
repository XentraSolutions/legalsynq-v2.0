using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xenia.Application;
using Xenia.Domain;

namespace Xenia.Infrastructure.Providers;

internal abstract class HttpAiProviderAdapterBase(IHttpClientFactory httpClientFactory) : IAiProviderAdapter
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public abstract bool CanHandle(XeniaProviderType providerType);

    public XeniaAiResponse Execute(XeniaProviderConfiguration provider, XeniaResolvedCredential credential, XeniaAiExecutionContext context)
    {
        var client = httpClientFactory.CreateClient(GetType().Name);
        client.Timeout = TimeSpan.FromSeconds(provider.TimeoutSeconds <= 0 ? 60 : provider.TimeoutSeconds);

        using var request = BuildRequest(provider, credential, context);
        var stopwatch = Stopwatch.StartNew();
        using var response = client.Send(request);
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"{provider.ProviderType} request failed with HTTP {(int)response.StatusCode}: {Trim(body)}");
        }

        return ParseResponse(provider, body, stopwatch.ElapsedMilliseconds, context.Prompt);
    }

    public virtual XeniaProviderValidationResult Validate(XeniaProviderConfiguration provider, XeniaResolvedCredential credential)
    {
        return new XeniaProviderValidationResult(
            true,
            "Connected",
            $"Credential metadata for '{provider.DisplayName}' is present and eligible for provider execution.",
            DateTime.UtcNow,
            credential.Fingerprint);
    }

    protected abstract HttpRequestMessage BuildRequest(XeniaProviderConfiguration provider, XeniaResolvedCredential credential, XeniaAiExecutionContext context);
    protected abstract XeniaAiResponse ParseResponse(XeniaProviderConfiguration provider, string body, long durationMs, string prompt);

    protected static IReadOnlyList<string> Chunk(string value)
    {
        const int chunkSize = 180;
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var chunks = new List<string>();
        for (var index = 0; index < value.Length; index += chunkSize)
            chunks.Add(value.Substring(index, Math.Min(chunkSize, value.Length - index)));
        return chunks;
    }

    protected static int EstimateTokens(string value) => Math.Max(1, value.Trim().Length / 4);

    protected static string Trim(string value) =>
        value.Length <= 400 ? value : value[..400];
}

internal sealed class OpenAiProviderAdapter(IHttpClientFactory httpClientFactory) : HttpAiProviderAdapterBase(httpClientFactory)
{
    public override bool CanHandle(XeniaProviderType providerType) => providerType == XeniaProviderType.OpenAI;

    protected override HttpRequestMessage BuildRequest(XeniaProviderConfiguration provider, XeniaResolvedCredential credential, XeniaAiExecutionContext context)
    {
        var endpoint = string.IsNullOrWhiteSpace(provider.Endpoint) ? "https://api.openai.com/v1/responses" : provider.Endpoint!.TrimEnd('/') + "/responses";
        var payload = JsonSerializer.Serialize(new
        {
            model = provider.DefaultModel,
            input = context.Prompt,
            temperature = 0.2,
        }, JsonOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.Secret);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        return request;
    }

    protected override XeniaAiResponse ParseResponse(XeniaProviderConfiguration provider, string body, long durationMs, string prompt)
    {
        using var document = JsonDocument.Parse(body);
        var output = TryGetOpenAiOutputText(document.RootElement);

        if (string.IsNullOrWhiteSpace(output))
            output = body;

        var promptTokens = document.RootElement.TryGetProperty("usage", out var usageElement)
            && usageElement.TryGetProperty("input_tokens", out var inputTokensElement)
            ? inputTokensElement.GetInt32()
            : EstimateTokens(prompt);

        var completionTokens = document.RootElement.TryGetProperty("usage", out usageElement)
            && usageElement.TryGetProperty("output_tokens", out var outputTokensElement)
            ? outputTokensElement.GetInt32()
            : EstimateTokens(output);

        return new XeniaAiResponse(
            provider.DisplayName,
            provider.DefaultModel,
            output!,
            Chunk(output!),
            promptTokens,
            completionTokens,
            Math.Round((promptTokens + completionTokens) * 0.00001m, 6),
            durationMs,
            true);
    }

    private static string? TryGetOpenAiOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputTextElement))
        {
            if (outputTextElement.ValueKind == JsonValueKind.String)
                return outputTextElement.GetString();

            if (outputTextElement.ValueKind == JsonValueKind.Array)
            {
                var joined = string.Join(
                    "\n",
                    outputTextElement.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString())
                        .Where(item => !string.IsNullOrWhiteSpace(item)));

                if (!string.IsNullOrWhiteSpace(joined))
                    return joined;
            }
        }

        if (!root.TryGetProperty("output", out var outputElement) || outputElement.ValueKind != JsonValueKind.Array)
            return null;

        var parts = new List<string>();

        foreach (var message in outputElement.EnumerateArray())
        {
            if (!message.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var content in contentElement.EnumerateArray())
            {
                if (!content.TryGetProperty("type", out var typeElement))
                    continue;

                var type = typeElement.GetString();
                if (!string.Equals(type, "output_text", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (content.TryGetProperty("text", out var textElement))
                {
                    var value = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        parts.Add(value);
                }
            }
        }

        return parts.Count == 0 ? null : string.Join("\n", parts);
    }
}

internal sealed class AzureOpenAiProviderAdapter(IHttpClientFactory httpClientFactory) : HttpAiProviderAdapterBase(httpClientFactory)
{
    public override bool CanHandle(XeniaProviderType providerType) => providerType == XeniaProviderType.AzureOpenAI;

    protected override HttpRequestMessage BuildRequest(XeniaProviderConfiguration provider, XeniaResolvedCredential credential, XeniaAiExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(provider.Endpoint) || string.IsNullOrWhiteSpace(provider.AzureDeploymentName))
            throw new InvalidOperationException("Azure OpenAI providers require both endpoint and deployment name.");

        var endpoint = $"{provider.Endpoint.TrimEnd('/')}/openai/deployments/{provider.AzureDeploymentName}/chat/completions?api-version=2024-06-01";
        var payload = JsonSerializer.Serialize(new
        {
            messages = new[] { new { role = "user", content = context.Prompt } },
            temperature = 0.2,
        }, JsonOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("api-key", credential.Secret);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        return request;
    }

    protected override XeniaAiResponse ParseResponse(XeniaProviderConfiguration provider, string body, long durationMs, string prompt)
    {
        using var document = JsonDocument.Parse(body);
        var choice = document.RootElement.GetProperty("choices")[0];
        var output = choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        var usage = document.RootElement.GetProperty("usage");
        var promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
        var completionTokens = usage.GetProperty("completion_tokens").GetInt32();

        return new XeniaAiResponse(
            provider.DisplayName,
            provider.DefaultModel,
            output,
            Chunk(output),
            promptTokens,
            completionTokens,
            Math.Round((promptTokens + completionTokens) * 0.00001m, 6),
            durationMs,
            true);
    }
}

internal sealed class AnthropicProviderAdapter(IHttpClientFactory httpClientFactory) : HttpAiProviderAdapterBase(httpClientFactory)
{
    public override bool CanHandle(XeniaProviderType providerType) => providerType == XeniaProviderType.Anthropic;

    protected override HttpRequestMessage BuildRequest(XeniaProviderConfiguration provider, XeniaResolvedCredential credential, XeniaAiExecutionContext context)
    {
        var endpoint = string.IsNullOrWhiteSpace(provider.Endpoint) ? "https://api.anthropic.com/v1/messages" : provider.Endpoint!.TrimEnd('/') + "/messages";
        var payload = JsonSerializer.Serialize(new
        {
            model = provider.DefaultModel,
            max_tokens = 1024,
            messages = new[] { new { role = "user", content = context.Prompt } },
        }, JsonOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-api-key", credential.Secret);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        return request;
    }

    protected override XeniaAiResponse ParseResponse(XeniaProviderConfiguration provider, string body, long durationMs, string prompt)
    {
        using var document = JsonDocument.Parse(body);
        var output = document.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
        var usage = document.RootElement.GetProperty("usage");
        var promptTokens = usage.TryGetProperty("input_tokens", out var inputTokens) ? inputTokens.GetInt32() : EstimateTokens(prompt);
        var completionTokens = usage.TryGetProperty("output_tokens", out var outputTokens) ? outputTokens.GetInt32() : EstimateTokens(output);

        return new XeniaAiResponse(
            provider.DisplayName,
            provider.DefaultModel,
            output,
            Chunk(output),
            promptTokens,
            completionTokens,
            Math.Round((promptTokens + completionTokens) * 0.00001m, 6),
            durationMs,
            true);
    }
}

internal sealed class GeminiProviderAdapter(IHttpClientFactory httpClientFactory) : HttpAiProviderAdapterBase(httpClientFactory)
{
    public override bool CanHandle(XeniaProviderType providerType) => providerType == XeniaProviderType.Gemini;

    protected override HttpRequestMessage BuildRequest(XeniaProviderConfiguration provider, XeniaResolvedCredential credential, XeniaAiExecutionContext context)
    {
        var baseEndpoint = string.IsNullOrWhiteSpace(provider.Endpoint)
            ? "https://generativelanguage.googleapis.com/v1beta/models"
            : provider.Endpoint!.TrimEnd('/');
        var endpoint = $"{baseEndpoint}/{provider.DefaultModel}:generateContent?key={Uri.EscapeDataString(credential.Secret)}";
        var payload = JsonSerializer.Serialize(new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = context.Prompt } },
                },
            },
        }, JsonOptions);

        return new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
    }

    protected override XeniaAiResponse ParseResponse(XeniaProviderConfiguration provider, string body, long durationMs, string prompt)
    {
        using var document = JsonDocument.Parse(body);
        var output = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        var promptTokens = EstimateTokens(prompt);
        var completionTokens = EstimateTokens(output);

        return new XeniaAiResponse(
            provider.DisplayName,
            provider.DefaultModel,
            output,
            Chunk(output),
            promptTokens,
            completionTokens,
            Math.Round((promptTokens + completionTokens) * 0.00001m, 6),
            durationMs,
            true);
    }
}

internal sealed class AwsBedrockProviderAdapter(IHttpClientFactory httpClientFactory) : HttpAiProviderAdapterBase(httpClientFactory)
{
    public override bool CanHandle(XeniaProviderType providerType) => providerType == XeniaProviderType.AwsBedrock;

    protected override HttpRequestMessage BuildRequest(XeniaProviderConfiguration provider, XeniaResolvedCredential credential, XeniaAiExecutionContext context) =>
        throw new NotSupportedException("AWS Bedrock execution requires SigV4 request signing and is not yet wired in this repo.");

    protected override XeniaAiResponse ParseResponse(XeniaProviderConfiguration provider, string body, long durationMs, string prompt) =>
        throw new NotSupportedException();

    public override XeniaProviderValidationResult Validate(XeniaProviderConfiguration provider, XeniaResolvedCredential credential) =>
        new(
            false,
            "NotSupported",
            $"Bedrock provider '{provider.DisplayName}' is configured, but request signing support has not been completed yet.",
            DateTime.UtcNow,
            credential.Fingerprint);
}
