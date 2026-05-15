using System.Net;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// Test-only HttpMessageHandler that records the requests it sees and
/// replies with a pre-canned response. TB-INT-02 extends it with
/// support for a per-request response sequence (for retry tests) and
/// per-request throw scripts.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;
    private int _calls;

    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> RequestBodies { get; } = new();
    public int CallCount => _calls;

    public FakeHttpMessageHandler(HttpStatusCode status, string? body = null)
        : this((_, _) => new HttpResponseMessage(status)
        {
            Content = body is null ? null : new StringContent(body),
        })
    {
    }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : this((req, _) => responder(req))
    {
    }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public static FakeHttpMessageHandler Throws(Exception ex)
        => new((_, _) => throw ex);

    /// <summary>
    /// Returns the i'th element of <paramref name="responses"/> on the
    /// i'th call (1-based). Once the script is exhausted, repeats the
    /// final element. Each item may be either an
    /// <see cref="HttpResponseMessage"/> (returned directly) or an
    /// <see cref="Exception"/> (re-thrown).
    /// </summary>
    public static FakeHttpMessageHandler Sequence(params object[] responses)
    {
        if (responses.Length == 0) throw new ArgumentException("at least one item required", nameof(responses));
        return new FakeHttpMessageHandler((_, attempt) =>
        {
            var idx = Math.Min(attempt - 1, responses.Length - 1);
            var item = responses[idx];
            return item switch
            {
                HttpResponseMessage r => r,
                HttpStatusCode s      => new HttpResponseMessage(s),
                Exception ex          => throw ex,
                _                     => throw new InvalidOperationException(
                    $"Unsupported sequence item type {item?.GetType().FullName}"),
            };
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content is not null)
        {
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }
        else
        {
            RequestBodies.Add(string.Empty);
        }
        var attempt = System.Threading.Interlocked.Increment(ref _calls);
        return _responder(request, attempt);
    }
}
