using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using Identity.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

public sealed class Route53DnsOptions
{
    public string HostedZoneId { get; set; } = string.Empty;
    public string BaseDomain { get; set; } = string.Empty;
    public string EnvironmentLabel { get; set; } = string.Empty;
    public string RecordType { get; set; } = "A";
    public string RecordValue { get; set; } = string.Empty;
    public string? TxtVerificationValue { get; set; }
    public long Ttl { get; set; } = 300;
    public string Region { get; set; } = "us-east-2";
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }
    public int ChangeWaitTimeoutSeconds { get; set; } = 120;
    public int ChangeWaitPollSeconds { get; set; } = 5;
}

public sealed class Route53DnsService : IDnsService, IDisposable
{
    private readonly IAmazonRoute53 _route53;
    private readonly Route53DnsOptions _opts;
    private readonly ILogger<Route53DnsService> _log;

    public string BaseDomain => _opts.BaseDomain;

    public Route53DnsService(IOptions<Route53DnsOptions> opts, ILogger<Route53DnsService> log)
    {
        _opts = opts.Value;
        _log = log;

        var config = new AmazonRoute53Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_opts.Region)
        };

        _route53 = _opts.AccessKeyId is not null
            ? new AmazonRoute53Client(_opts.AccessKeyId, _opts.SecretAccessKey, config)
            : new AmazonRoute53Client(config);
    }

    public async Task<bool> CreateSubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        var fqdn = BuildHostname(subdomain);
        await DeleteConflictingRecordsAsync(fqdn, ct);

        var aChangeId = await UpsertRecordAsync(fqdn, ChangeAction.UPSERT, ct);
        var aSuccess = !string.IsNullOrWhiteSpace(aChangeId) &&
            await WaitForChangeAsync(aChangeId, fqdn, ct);

        if (aSuccess && !string.IsNullOrWhiteSpace(_opts.TxtVerificationValue))
        {
            var txtChangeId = await UpsertTxtRecordAsync(fqdn, ChangeAction.UPSERT, ct);
            var txtSuccess = !string.IsNullOrWhiteSpace(txtChangeId) &&
                await WaitForChangeAsync(txtChangeId, fqdn, ct);
            if (!txtSuccess)
                _log.LogWarning("A record created but TXT verification record failed for {Fqdn}", fqdn);
        }

        return aSuccess;
    }

    public async Task<bool> DeleteSubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        var fqdn = BuildHostname(subdomain);
        var changeId = await UpsertRecordAsync(fqdn, ChangeAction.DELETE, ct);
        var deleted = !string.IsNullOrWhiteSpace(changeId) &&
            await WaitForChangeAsync(changeId, fqdn, ct);

        if (!string.IsNullOrWhiteSpace(_opts.TxtVerificationValue))
        {
            var txtChangeId = await UpsertTxtRecordAsync(fqdn, ChangeAction.DELETE, ct);
            if (!string.IsNullOrWhiteSpace(txtChangeId))
                await WaitForChangeAsync(txtChangeId, fqdn, ct);
        }

        return deleted;
    }

    public string BuildHostname(string tenantSlug)
    {
        var slug = NormalizeLabel(tenantSlug, nameof(tenantSlug));
        var environment = string.IsNullOrWhiteSpace(_opts.EnvironmentLabel)
            ? null : NormalizeLabel(_opts.EnvironmentLabel, nameof(_opts.EnvironmentLabel));
        var domain = _opts.BaseDomain.Trim().Trim('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(domain) || domain.Split('.').Any(x => !IsValidLabel(x)))
            throw new InvalidOperationException("Route53 BaseDomain is not a valid DNS name.");
        if (environment is not null && (slug == environment || slug.EndsWith($".{environment}", StringComparison.Ordinal)))
            throw new ArgumentException("Tenant slug must not include the configured environment label.", nameof(tenantSlug));
        return environment is null ? $"{slug}.{domain}" : $"{slug}.{environment}.{domain}";
    }

    private static string NormalizeLabel(string value, string parameter)
    {
        var label = value.Trim().Trim('.').ToLowerInvariant();
        if (!IsValidLabel(label)) throw new ArgumentException("Value must be a valid DNS label.", parameter);
        return label;
    }

    private static bool IsValidLabel(string label) => label.Length is >= 1 and <= 63 &&
        char.IsAsciiLetterOrDigit(label[0]) && char.IsAsciiLetterOrDigit(label[^1]) &&
        label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    private async Task DeleteConflictingRecordsAsync(string fqdn, CancellationToken ct)
    {
        var targetType = RRType.FindValue(_opts.RecordType);
        var conflictTypes = new[] { RRType.CNAME, RRType.A, RRType.AAAA }
            .Where(t => t != targetType)
            .ToList();

        try
        {
            var listRequest = new ListResourceRecordSetsRequest
            {
                HostedZoneId = _opts.HostedZoneId,
                StartRecordName = fqdn,
                MaxItems = "10"
            };

            var listResponse = await _route53.ListResourceRecordSetsAsync(listRequest, ct);
            var changes = new List<Change>();

            foreach (var rrs in listResponse.ResourceRecordSets)
            {
                if (!string.Equals(rrs.Name.TrimEnd('.'), fqdn.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
                    continue;

                if (conflictTypes.Contains(rrs.Type))
                {
                    _log.LogInformation("Deleting conflicting {Type} record for {Fqdn}", rrs.Type.Value, fqdn);
                    changes.Add(new Change
                    {
                        Action = ChangeAction.DELETE,
                        ResourceRecordSet = rrs
                    });
                }
            }

            if (changes.Count > 0)
            {
                var deleteRequest = new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = _opts.HostedZoneId,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = changes,
                        Comment = $"LegalSynq: removing conflicting records before creating {targetType.Value} for {fqdn}"
                    }
                };
                await _route53.ChangeResourceRecordSetsAsync(deleteRequest, ct);
                _log.LogInformation("Deleted {Count} conflicting record(s) for {Fqdn}", changes.Count, fqdn);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not check/delete conflicting records for {Fqdn} — proceeding with upsert", fqdn);
        }
    }

    private async Task<string?> UpsertRecordAsync(string fqdn, ChangeAction action, CancellationToken ct)
    {
        try
        {
            var change = new Change
            {
                Action = action,
                ResourceRecordSet = new ResourceRecordSet
                {
                    Name = fqdn,
                    Type = RRType.FindValue(_opts.RecordType),
                    TTL = _opts.Ttl,
                    ResourceRecords = new List<ResourceRecord>
                    {
                        new ResourceRecord { Value = _opts.RecordValue }
                    }
                }
            };

            var request = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = _opts.HostedZoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change> { change },
                    Comment = $"LegalSynq tenant subdomain: {action.Value} {fqdn}"
                }
            };

            var response = await _route53.ChangeResourceRecordSetsAsync(request, ct);
            _log.LogInformation(
                "Route53 {Action} {Type} for {Fqdn}: status={Status}, changeId={ChangeId}",
                action.Value, _opts.RecordType, fqdn, response.ChangeInfo.Status.Value, response.ChangeInfo.Id);

            return response.ChangeInfo.Id;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Route53 {Action} failed for {Fqdn}", action.Value, fqdn);
            return null;
        }
    }

    private async Task<string?> UpsertTxtRecordAsync(string fqdn, ChangeAction action, CancellationToken ct)
    {
        try
        {
            var txtValue = $"\"{_opts.TxtVerificationValue}\"";
            var change = new Change
            {
                Action = action,
                ResourceRecordSet = new ResourceRecordSet
                {
                    Name = fqdn,
                    Type = RRType.TXT,
                    TTL = _opts.Ttl,
                    ResourceRecords = new List<ResourceRecord>
                    {
                        new ResourceRecord { Value = txtValue }
                    }
                }
            };

            var request = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = _opts.HostedZoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change> { change },
                    Comment = $"LegalSynq tenant TXT verification: {action.Value} {fqdn}"
                }
            };

            var response = await _route53.ChangeResourceRecordSetsAsync(request, ct);
            _log.LogInformation(
                "Route53 {Action} TXT for {Fqdn}: status={Status}, changeId={ChangeId}",
                action.Value, fqdn, response.ChangeInfo.Status.Value, response.ChangeInfo.Id);

            return response.ChangeInfo.Id;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Route53 TXT {Action} failed for {Fqdn}", action.Value, fqdn);
            return null;
        }
    }

    private async Task<bool> WaitForChangeAsync(string changeId, string fqdn, CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _opts.ChangeWaitTimeoutSeconds));
        var poll = TimeSpan.FromSeconds(Math.Max(1, _opts.ChangeWaitPollSeconds));
        var started = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            try
            {
                var response = await _route53.GetChangeAsync(new GetChangeRequest
                {
                    Id = changeId
                }, ct);

                var status = response.ChangeInfo.Status;
                if (status == ChangeStatus.INSYNC)
                {
                    _log.LogInformation(
                        "Route53 change {ChangeId} for {Fqdn} is INSYNC",
                        changeId, fqdn);
                    return true;
                }

                _log.LogDebug(
                    "Route53 change {ChangeId} for {Fqdn} is {Status}; waiting",
                    changeId, fqdn, status.Value);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "Route53 change wait failed while checking {ChangeId} for {Fqdn}",
                    changeId, fqdn);
                return false;
            }

            await Task.Delay(poll, ct);
        }

        _log.LogWarning(
            "Route53 change {ChangeId} for {Fqdn} did not become INSYNC within {TimeoutSeconds}s",
            changeId, fqdn, _opts.ChangeWaitTimeoutSeconds);
        return false;
    }

    public void Dispose()
    {
        _route53.Dispose();
    }
}
