using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Xenia.Application.Assistant;
using Xenia.Application.TenantContext;

namespace Xenia.Infrastructure.Assistant;

internal sealed class OpenAiAssistantProvider
{
    private static readonly HttpClient SharedHttpClient = new();

    private readonly IAssistantRuntimeSettingsService _settings;
    private readonly XeniaTenantContextAccessor _tenantAccessor;

    public OpenAiAssistantProvider(
        IAssistantRuntimeSettingsService settings,
        XeniaTenantContextAccessor tenantAccessor)
    {
        _settings = settings;
        _tenantAccessor = tenantAccessor;
    }

    public async IAsyncEnumerable<AssistantProviderEvent> StreamAsync(
        AssistantProviderRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var settings = await _settings.GetEffectiveSettingsAsync(
            _tenantAccessor.Current?.IsResolved == true
                ? _tenantAccessor.Current.TenantId
                : null,
            ct);

        var apiKey = settings.OpenAiApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            yield return new AssistantProviderEvent("error", SafeError: "OpenAI API key is not configured in Xenia appsettings.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(request.ModelKey))
        {
            yield return new AssistantProviderEvent("error", SafeError: "OpenAI model key is not configured.");
            yield break;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{settings.OpenAiBaseUrl.TrimEnd('/')}/v1/responses");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var input = new List<object>
        {
            new { role = "system", content = request.SystemPrompt },
        };
        input.AddRange(request.Messages.Select(m => new
        {
            role = m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
            content = m.Content,
        }));

        var body = new Dictionary<string, object?>
        {
            ["model"] = request.ModelKey,
            ["input"] = input,
            ["stream"] = true,
        };

        if (!string.IsNullOrWhiteSpace(settings.OpenAiReasoningEffort))
        {
            body["reasoning"] = new Dictionary<string, object?>
            {
                ["effort"] = settings.OpenAiReasoningEffort,
            };
        }

        if (!string.IsNullOrWhiteSpace(settings.OpenAiTextVerbosity))
        {
            body["text"] = new Dictionary<string, object?>
            {
                ["verbosity"] = settings.OpenAiTextVerbosity,
            };
        }

        if (settings.OpenAiMaxOutputTokens is > 0)
            body["max_output_tokens"] = settings.OpenAiMaxOutputTokens.Value;

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, settings.OpenAiTimeoutSeconds)));

        using var response = await SharedHttpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token);

        if (!response.IsSuccessStatusCode)
        {
            yield return new AssistantProviderEvent("error", SafeError: $"OpenAI request failed with HTTP {(int)response.StatusCode}.");
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            timeoutCts.Token.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(timeoutCts.Token);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            var payload = line["data:".Length..].Trim();
            if (payload == "[DONE]") yield break;

            AssistantProviderEvent? parsed = TryParseResponseEvent(payload);
            if (parsed is not null) yield return parsed;
        }
    }

    private static AssistantProviderEvent? TryParseResponseEvent(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;

            if (type == "response.created")
            {
                return new AssistantProviderEvent(
                    "created",
                    ProviderResponseId: root.TryGetProperty("response", out var response) &&
                                        response.TryGetProperty("id", out var id)
                                            ? id.GetString()
                                            : null);
            }

            if (type == "response.output_text.delta")
            {
                return new AssistantProviderEvent(
                    "delta",
                    Delta: root.TryGetProperty("delta", out var delta) ? delta.GetString() : null);
            }

            if (type == "response.completed")
            {
                var completedResponse = root.TryGetProperty("response", out var response)
                    ? response
                    : default;

                return new AssistantProviderEvent(
                    "completed",
                    ProviderResponseId: completedResponse.ValueKind != JsonValueKind.Undefined &&
                                        completedResponse.TryGetProperty("id", out var completedId)
                                            ? completedId.GetString()
                                            : null,
                    InputTokens: TryGetUsage(completedResponse, "input_tokens"),
                    OutputTokens: TryGetUsage(completedResponse, "output_tokens"),
                    FinishReason: "stop");
            }

            return type == "error"
                ? new AssistantProviderEvent("error", SafeError: "OpenAI returned a streaming error.")
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? TryGetUsage(JsonElement response, string propertyName)
    {
        if (response.ValueKind == JsonValueKind.Undefined) return null;
        if (!response.TryGetProperty("usage", out var usage)) return null;
        return usage.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : null;
    }
}
