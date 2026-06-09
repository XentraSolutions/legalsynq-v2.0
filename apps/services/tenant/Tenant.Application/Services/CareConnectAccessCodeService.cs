using System.Security.Cryptography;
using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public class CareConnectAccessCodeService : ICareConnectAccessCodeService
{
    private const string ProductKey = "careconnect";
    private const string HashKey = "careconnect.public-network.access-code.hash";
    private const string VersionKey = "careconnect.public-network.access-code.version";
    private const int MinLength = 8;
    private const int MaxLength = 128;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int IterationCount = 100_000;

    private readonly ISettingRepository _settings;
    private readonly ITenantRepository _tenants;

    public CareConnectAccessCodeService(ISettingRepository settings, ITenantRepository tenants)
    {
        _settings = settings;
        _tenants = tenants;
    }

    public async Task<CareConnectAccessCodeMetadataResponse> GetMetadataAsync(Guid tenantId, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var state = await LoadStateAsync(tenantId, ct);
        return new CareConnectAccessCodeMetadataResponse(
            state.Hash is not null,
            state.Version,
            state.Hash?.UpdatedAtUtc);
    }

    public async Task<SetCareConnectAccessCodeResponse> SetAsync(Guid tenantId, string code, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);

        var normalizedCode = NormalizeCode(code);
        ValidateCode(normalizedCode);

        var state = await LoadStateAsync(tenantId, ct);
        var version = checked(state.Version + 1);
        var hashValue = HashCode(normalizedCode);
        TenantSetting hashSetting;

        if (state.Hash is null)
        {
            hashSetting = TenantSetting.Create(tenantId, HashKey, hashValue, SettingValueType.String, ProductKey);
            await _settings.AddAsync(hashSetting, ct);
        }
        else
        {
            hashSetting = state.Hash;
            hashSetting.UpdateValue(hashValue, SettingValueType.String);
            await _settings.UpdateAsync(hashSetting, ct);
        }

        await UpsertVersionAsync(tenantId, state.VersionSetting, version, ct);

        return new SetCareConnectAccessCodeResponse(
            true,
            version,
            hashSetting.UpdatedAtUtc,
            normalizedCode);
    }

    public async Task<CareConnectAccessCodeMetadataResponse> ClearAsync(Guid tenantId, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);

        var state = await LoadStateAsync(tenantId, ct);
        var version = checked(state.Version + 1);

        if (state.Hash is not null)
        {
            await _settings.DeleteAsync(state.Hash, ct);
        }

        await UpsertVersionAsync(tenantId, state.VersionSetting, version, ct);

        return new CareConnectAccessCodeMetadataResponse(false, version, null);
    }

    public async Task<CareConnectAccessCodeStatusResponse> GetStatusAsync(Guid tenantId, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);
        var state = await LoadStateAsync(tenantId, ct);
        return new CareConnectAccessCodeStatusResponse(state.Hash is not null, state.Version);
    }

    public async Task<VerifyCareConnectAccessCodeResponse> VerifyAsync(Guid tenantId, string code, CancellationToken ct = default)
    {
        await RequireTenantAsync(tenantId, ct);

        var state = await LoadStateAsync(tenantId, ct);
        if (state.Hash is null)
        {
            return new VerifyCareConnectAccessCodeResponse(false, false, state.Version);
        }

        var normalizedCode = NormalizeCode(code);
        var ok = VerifyHash(normalizedCode, state.Hash.SettingValue);
        return new VerifyCareConnectAccessCodeResponse(ok, true, state.Version);
    }

    private async Task RequireTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null)
        {
            throw new NotFoundException($"Tenant '{tenantId}' was not found.");
        }
    }

    private async Task<AccessCodeState> LoadStateAsync(Guid tenantId, CancellationToken ct)
    {
        var hash = await _settings.GetByKeyAsync(tenantId, HashKey, ProductKey, ct);
        var versionSetting = await _settings.GetByKeyAsync(tenantId, VersionKey, ProductKey, ct);

        var version = 0;
        if (versionSetting is not null && !int.TryParse(versionSetting.SettingValue, out version))
        {
            throw new ValidationException(
                "Stored access-code version is invalid.",
                new Dictionary<string, string[]> { ["version"] = ["Access-code version must be a valid integer."] });
        }

        return new AccessCodeState(hash, versionSetting, version);
    }

    private async Task UpsertVersionAsync(
        Guid tenantId,
        TenantSetting? versionSetting,
        int version,
        CancellationToken ct)
    {
        if (versionSetting is null)
        {
            var created = TenantSetting.Create(
                tenantId,
                VersionKey,
                version.ToString(),
                SettingValueType.Number,
                ProductKey);
            await _settings.AddAsync(created, ct);
            return;
        }

        versionSetting.UpdateValue(version.ToString(), SettingValueType.Number);
        await _settings.UpdateAsync(versionSetting, ct);
    }

    private static string NormalizeCode(string code) => (code ?? string.Empty).Trim();

    private static void ValidateCode(string code)
    {
        var errors = new Dictionary<string, string[]>();
        if (code.Length < MinLength)
        {
            errors["code"] = [$"Access code must be at least {MinLength} characters."];
        }
        else if (code.Length > MaxLength)
        {
            errors["code"] = [$"Access code must be at most {MaxLength} characters."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("Access code is invalid.", errors);
        }
    }

    private static string HashCode(string code)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            code,
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            HashSizeBytes);

        return string.Join(
            '$',
            "pbkdf2-sha256",
            IterationCount.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    private static bool VerifyHash(string code, string storedHash)
    {
        var parts = storedHash.Split('$', StringSplitOptions.None);
        if (parts.Length != 4 || !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            code,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private sealed record AccessCodeState(
        TenantSetting? Hash,
        TenantSetting? VersionSetting,
        int Version);
}
