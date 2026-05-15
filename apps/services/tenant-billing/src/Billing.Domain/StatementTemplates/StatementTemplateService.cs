using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.StatementTemplates;

/// <summary>
/// STAT-B02 — Default <see cref="IStatementTemplateService"/>.
/// Mirrors <c>InvoiceTemplateService</c>'s lifecycle + default-
/// uniqueness flow but tenant-scoped only (no platform overloads).
/// </summary>
public sealed class StatementTemplateService :
    IStatementTemplateService,
    IStatementTemplateSelectionService
{
    private readonly IStatementTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public StatementTemplateService(
        IStatementTemplateRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider? time = null)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _time = time ?? TimeProvider.System;
    }

    // -----------------------------------------------------------------
    // Reads
    // -----------------------------------------------------------------

    public Task<StatementTemplate?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        ValidateTenant(tenantId);
        if (id == Guid.Empty)
            throw new ArgumentException("Template id is required.", nameof(id));
        return _repository.GetByIdInScopeReadOnlyAsync(tenantId, id, ct);
    }

    public Task<IReadOnlyList<StatementTemplate>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        ValidateTenant(tenantId);
        return _repository.ListInScopeAsync(tenantId, ct);
    }

    public Task<StatementTemplate?> GetDefaultAsync(Guid tenantId, CancellationToken ct = default)
    {
        ValidateTenant(tenantId);
        return _repository.GetDefaultInScopeAsync(tenantId, ct);
    }

    public async Task<StatementTemplate?> SelectForStatementAsync(
        Guid tenantId, Guid? explicitTemplateId, CancellationToken ct = default)
    {
        ValidateTenant(tenantId);

        if (explicitTemplateId is null)
            return await _repository.GetDefaultInScopeAsync(tenantId, ct);

        var id = explicitTemplateId.Value;
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Explicit statement template id must be a non-empty GUID.",
                nameof(explicitTemplateId));

        var template = await _repository.GetByIdInScopeReadOnlyAsync(tenantId, id, ct);
        if (template is null)
            throw new StatementTemplateNotFoundInScopeException(id);

        if (template.Status != StatementTemplateStatus.Active)
            throw new StatementTemplateNotSelectableException(id, template.Status);

        return template;
    }

    // -----------------------------------------------------------------
    // Create
    // -----------------------------------------------------------------

    public async Task<StatementTemplate> CreateAsync(Guid tenantId, NewStatementTemplate input, CancellationToken ct = default)
    {
        ValidateTenant(tenantId);
        ArgumentNullException.ThrowIfNull(input);

        var status = string.IsNullOrWhiteSpace(input.Status)
            ? StatementTemplateStatus.Draft
            : StatementTemplateValidation.ValidateStatus(input.Status);

        var now = _time.GetUtcNow().UtcDateTime;
        var template = new StatementTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = StatementTemplateValidation.NormalizeName(input.Name),
            Description = StatementTemplateValidation.NormalizeOptionalText(
                input.Description, StatementTemplateValidation.DescriptionMaxLength, nameof(input.Description)),
            Status = status,
            IsDefault = false,
            LogoUrl = StatementTemplateValidation.NormalizeLogoUrl(input.LogoUrl),
            AccentColor = StatementTemplateValidation.NormalizeAccentColor(input.AccentColor),
            HeaderText = StatementTemplateValidation.NormalizeOptionalText(
                input.HeaderText, StatementTemplateValidation.HeaderTextMaxLength, nameof(input.HeaderText)),
            FooterText = StatementTemplateValidation.NormalizeOptionalText(
                input.FooterText, StatementTemplateValidation.FooterTextMaxLength, nameof(input.FooterText)),
            PaymentInstructions = StatementTemplateValidation.NormalizeOptionalText(
                input.PaymentInstructions, StatementTemplateValidation.PaymentInstructionsMaxLength, nameof(input.PaymentInstructions)),
            TermsText = StatementTemplateValidation.NormalizeOptionalText(
                input.TermsText, StatementTemplateValidation.TermsTextMaxLength, nameof(input.TermsText)),
            MemoPlaceholder = StatementTemplateValidation.NormalizeOptionalText(
                input.MemoPlaceholder, StatementTemplateValidation.MemoPlaceholderMaxLength, nameof(input.MemoPlaceholder)),
            DisplayOutstandingTable = input.DisplayOutstandingTable ?? true,
            DisplayPaymentInstructions = input.DisplayPaymentInstructions ?? true,
            DisplayTransactionMemos = input.DisplayTransactionMemos ?? true,
            StatementNumberPrefix = StatementTemplateValidation.NormalizeStatementNumberPrefix(input.StatementNumberPrefix),
            IssuerDisplayName = StatementTemplateValidation.NormalizeOptionalText(
                input.IssuerDisplayName, StatementTemplateValidation.IssuerDisplayNameMaxLength, nameof(input.IssuerDisplayName)),
            IssuerLegalName = StatementTemplateValidation.NormalizeOptionalText(
                input.IssuerLegalName, StatementTemplateValidation.IssuerLegalNameMaxLength, nameof(input.IssuerLegalName)),
            IssuerAddressLine1 = StatementTemplateValidation.NormalizeOptionalText(
                input.IssuerAddressLine1, StatementTemplateValidation.IssuerAddressLineMaxLength, nameof(input.IssuerAddressLine1)),
            IssuerAddressLine2 = StatementTemplateValidation.NormalizeOptionalText(
                input.IssuerAddressLine2, StatementTemplateValidation.IssuerAddressLineMaxLength, nameof(input.IssuerAddressLine2)),
            IssuerCity = StatementTemplateValidation.NormalizeOptionalText(
                input.IssuerCity, StatementTemplateValidation.IssuerCityMaxLength, nameof(input.IssuerCity)),
            IssuerStateRegion = StatementTemplateValidation.NormalizeOptionalText(
                input.IssuerStateRegion, StatementTemplateValidation.IssuerStateRegionMaxLength, nameof(input.IssuerStateRegion)),
            IssuerPostalCode = StatementTemplateValidation.NormalizeOptionalText(
                input.IssuerPostalCode, StatementTemplateValidation.IssuerPostalCodeMaxLength, nameof(input.IssuerPostalCode)),
            IssuerCountry = StatementTemplateValidation.NormalizeOptionalText(
                input.IssuerCountry, StatementTemplateValidation.IssuerCountryMaxLength, nameof(input.IssuerCountry)),
            IssuerEmail = StatementTemplateValidation.NormalizeIssuerEmail(input.IssuerEmail),
            IssuerPhone = StatementTemplateValidation.NormalizeOptionalText(
                input.IssuerPhone, StatementTemplateValidation.IssuerPhoneMaxLength, nameof(input.IssuerPhone)),
            IssuerTaxId = StatementTemplateValidation.NormalizeOptionalText(
                input.IssuerTaxId, StatementTemplateValidation.IssuerTaxIdMaxLength, nameof(input.IssuerTaxId)),
            IssuerWebsite = StatementTemplateValidation.NormalizeIssuerWebsite(input.IssuerWebsite),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var explicitDefault = input.IsDefault == true;
        var autoDefault = !explicitDefault
            && status == StatementTemplateStatus.Active
            && !await _repository.AnyDefaultInScopeAsync(tenantId, ct);

        if (explicitDefault && status != StatementTemplateStatus.Active)
            throw new InvalidStatementTemplateStatusTransitionException(status, "Default");

        if (explicitDefault || autoDefault)
        {
            // Atomic-default flow: open transaction, add row, unset
            // peers, set self, commit. Same shape as InvoiceTemplate
            // so behaviour matches the field-tested pattern.
            await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
            await _repository.AddAsync(template, ct);
            await _repository.UnsetDefaultsInScopeAsync(tenantId, template.Id, template.UpdatedAtUtc, ct);
            template.IsDefault = true;
            await _repository.UpdateAsync(template, ct);
            await tx.CommitAsync(ct);
            return template;
        }

        return await _repository.AddAsync(template, ct);
    }

    // -----------------------------------------------------------------
    // Update / lifecycle
    // -----------------------------------------------------------------

    public async Task<StatementTemplate?> UpdateAsync(Guid tenantId, Guid id, StatementTemplateUpdate update, CancellationToken ct = default)
    {
        ValidateTenant(tenantId);
        if (id == Guid.Empty)
            throw new ArgumentException("Template id is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(update);

        var existing = await _repository.GetByIdInScopeAsync(tenantId, id, ct);
        if (existing is null) return null;

        if (existing.Status == StatementTemplateStatus.Retired)
            throw new InvalidStatementTemplateStatusTransitionException(existing.Status, "Edited");

        if (update.Name is not null)
            existing.Name = StatementTemplateValidation.NormalizeName(update.Name);
        if (update.Description is not null)
            existing.Description = StatementTemplateValidation.NormalizeOptionalText(
                update.Description, StatementTemplateValidation.DescriptionMaxLength, nameof(update.Description));
        if (update.LogoUrl is not null)
            existing.LogoUrl = StatementTemplateValidation.NormalizeLogoUrl(update.LogoUrl);
        if (update.AccentColor is not null)
            existing.AccentColor = StatementTemplateValidation.NormalizeAccentColor(update.AccentColor);
        if (update.HeaderText is not null)
            existing.HeaderText = StatementTemplateValidation.NormalizeOptionalText(
                update.HeaderText, StatementTemplateValidation.HeaderTextMaxLength, nameof(update.HeaderText));
        if (update.FooterText is not null)
            existing.FooterText = StatementTemplateValidation.NormalizeOptionalText(
                update.FooterText, StatementTemplateValidation.FooterTextMaxLength, nameof(update.FooterText));
        if (update.PaymentInstructions is not null)
            existing.PaymentInstructions = StatementTemplateValidation.NormalizeOptionalText(
                update.PaymentInstructions, StatementTemplateValidation.PaymentInstructionsMaxLength, nameof(update.PaymentInstructions));
        if (update.TermsText is not null)
            existing.TermsText = StatementTemplateValidation.NormalizeOptionalText(
                update.TermsText, StatementTemplateValidation.TermsTextMaxLength, nameof(update.TermsText));
        if (update.MemoPlaceholder is not null)
            existing.MemoPlaceholder = StatementTemplateValidation.NormalizeOptionalText(
                update.MemoPlaceholder, StatementTemplateValidation.MemoPlaceholderMaxLength, nameof(update.MemoPlaceholder));
        if (update.DisplayOutstandingTable is not null)
            existing.DisplayOutstandingTable = update.DisplayOutstandingTable.Value;
        if (update.DisplayPaymentInstructions is not null)
            existing.DisplayPaymentInstructions = update.DisplayPaymentInstructions.Value;
        if (update.DisplayTransactionMemos is not null)
            existing.DisplayTransactionMemos = update.DisplayTransactionMemos.Value;
        if (update.StatementNumberPrefix is not null)
            existing.StatementNumberPrefix = StatementTemplateValidation.NormalizeStatementNumberPrefix(update.StatementNumberPrefix);
        if (update.IssuerDisplayName is not null)
            existing.IssuerDisplayName = StatementTemplateValidation.NormalizeOptionalText(
                update.IssuerDisplayName, StatementTemplateValidation.IssuerDisplayNameMaxLength, nameof(update.IssuerDisplayName));
        if (update.IssuerLegalName is not null)
            existing.IssuerLegalName = StatementTemplateValidation.NormalizeOptionalText(
                update.IssuerLegalName, StatementTemplateValidation.IssuerLegalNameMaxLength, nameof(update.IssuerLegalName));
        if (update.IssuerAddressLine1 is not null)
            existing.IssuerAddressLine1 = StatementTemplateValidation.NormalizeOptionalText(
                update.IssuerAddressLine1, StatementTemplateValidation.IssuerAddressLineMaxLength, nameof(update.IssuerAddressLine1));
        if (update.IssuerAddressLine2 is not null)
            existing.IssuerAddressLine2 = StatementTemplateValidation.NormalizeOptionalText(
                update.IssuerAddressLine2, StatementTemplateValidation.IssuerAddressLineMaxLength, nameof(update.IssuerAddressLine2));
        if (update.IssuerCity is not null)
            existing.IssuerCity = StatementTemplateValidation.NormalizeOptionalText(
                update.IssuerCity, StatementTemplateValidation.IssuerCityMaxLength, nameof(update.IssuerCity));
        if (update.IssuerStateRegion is not null)
            existing.IssuerStateRegion = StatementTemplateValidation.NormalizeOptionalText(
                update.IssuerStateRegion, StatementTemplateValidation.IssuerStateRegionMaxLength, nameof(update.IssuerStateRegion));
        if (update.IssuerPostalCode is not null)
            existing.IssuerPostalCode = StatementTemplateValidation.NormalizeOptionalText(
                update.IssuerPostalCode, StatementTemplateValidation.IssuerPostalCodeMaxLength, nameof(update.IssuerPostalCode));
        if (update.IssuerCountry is not null)
            existing.IssuerCountry = StatementTemplateValidation.NormalizeOptionalText(
                update.IssuerCountry, StatementTemplateValidation.IssuerCountryMaxLength, nameof(update.IssuerCountry));
        if (update.IssuerEmail is not null)
            existing.IssuerEmail = StatementTemplateValidation.NormalizeIssuerEmail(update.IssuerEmail);
        if (update.IssuerPhone is not null)
            existing.IssuerPhone = StatementTemplateValidation.NormalizeOptionalText(
                update.IssuerPhone, StatementTemplateValidation.IssuerPhoneMaxLength, nameof(update.IssuerPhone));
        if (update.IssuerTaxId is not null)
            existing.IssuerTaxId = StatementTemplateValidation.NormalizeOptionalText(
                update.IssuerTaxId, StatementTemplateValidation.IssuerTaxIdMaxLength, nameof(update.IssuerTaxId));
        if (update.IssuerWebsite is not null)
            existing.IssuerWebsite = StatementTemplateValidation.NormalizeIssuerWebsite(update.IssuerWebsite);

        existing.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _repository.UpdateAsync(existing, ct);
        return existing;
    }

    public async Task<StatementTemplate?> ActivateAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        ValidateTenant(tenantId);
        if (id == Guid.Empty)
            throw new ArgumentException("Template id is required.", nameof(id));

        var existing = await _repository.GetByIdInScopeAsync(tenantId, id, ct);
        if (existing is null) return null;

        if (existing.Status == StatementTemplateStatus.Retired)
            throw new InvalidStatementTemplateStatusTransitionException(existing.Status, StatementTemplateStatus.Active);

        if (existing.Status == StatementTemplateStatus.Active)
            return existing;

        existing.Status = StatementTemplateStatus.Active;
        existing.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _repository.UpdateAsync(existing, ct);
        return existing;
    }

    public async Task<StatementTemplate?> RetireAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        ValidateTenant(tenantId);
        if (id == Guid.Empty)
            throw new ArgumentException("Template id is required.", nameof(id));

        var existing = await _repository.GetByIdInScopeAsync(tenantId, id, ct);
        if (existing is null) return null;

        if (existing.Status == StatementTemplateStatus.Retired)
            return existing;

        existing.Status = StatementTemplateStatus.Retired;
        existing.IsDefault = false;
        existing.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _repository.UpdateAsync(existing, ct);
        return existing;
    }

    public async Task<StatementTemplate?> MakeDefaultAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        ValidateTenant(tenantId);
        if (id == Guid.Empty)
            throw new ArgumentException("Template id is required.", nameof(id));

        var existing = await _repository.GetByIdInScopeAsync(tenantId, id, ct);
        if (existing is null) return null;

        if (existing.Status == StatementTemplateStatus.Retired)
            throw new RetiredStatementTemplateCannotBeDefaultException(id);
        if (existing.Status == StatementTemplateStatus.Draft)
            throw new InvalidStatementTemplateStatusTransitionException(existing.Status, "Default");

        if (existing.IsDefault) return existing;

        var now = _time.GetUtcNow().UtcDateTime;

        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
        await _repository.UnsetDefaultsInScopeAsync(tenantId, id, now, ct);
        existing.IsDefault = true;
        existing.UpdatedAtUtc = now;
        await _repository.UpdateAsync(existing, ct);
        await tx.CommitAsync(ct);
        return existing;
    }

    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
    }
}
