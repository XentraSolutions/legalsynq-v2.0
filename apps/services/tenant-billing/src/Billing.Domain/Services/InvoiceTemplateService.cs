using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.Services;

public sealed class InvoiceTemplateService : IInvoiceTemplateService, IInvoiceTemplateSelectionService
{
    private readonly IInvoiceTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public InvoiceTemplateService(
        IInvoiceTemplateRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    // -----------------------------------------------------------------
    // Reads
    // -----------------------------------------------------------------

    public Task<InvoiceTemplate?> GetAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Template id is required.", nameof(id));
        ValidateTenantScopeShape(tenantId);
        return _repository.GetByIdInScopeReadOnlyAsync(tenantId, id, ct);
    }

    public Task<IReadOnlyList<InvoiceTemplate>> ListAsync(Guid? tenantId, CancellationToken ct = default)
    {
        ValidateTenantScopeShape(tenantId);
        return _repository.ListInScopeAsync(tenantId, ct);
    }

    public Task<InvoiceTemplate?> GetDefaultAsync(Guid? tenantId, CancellationToken ct = default)
    {
        ValidateTenantScopeShape(tenantId);
        return _repository.GetDefaultInScopeAsync(tenantId, ct);
    }

    // IInvoiceTemplateSelectionService
    public Task<InvoiceTemplate?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return _repository.GetDefaultInScopeAsync(tenantId, ct);
    }

    public Task<InvoiceTemplate?> GetDefaultPlatformAsync(CancellationToken ct = default)
        => _repository.GetDefaultInScopeAsync(tenantId: null, ct);

    public async Task<InvoiceTemplate?> SelectForTenantInvoiceAsync(
        Guid tenantId, Guid? explicitTemplateId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return await SelectForScopeAsync(tenantId, explicitTemplateId, ct);
    }

    public Task<InvoiceTemplate?> SelectForPlatformInvoiceAsync(
        Guid? explicitTemplateId, CancellationToken ct = default)
        => SelectForScopeAsync(tenantId: null, explicitTemplateId, ct);

    /// <summary>
    /// Shared internal: pick the effective template for a (possibly
    /// platform) scope, validating an explicit override or falling
    /// back to the scope's active default. See
    /// <see cref="IInvoiceTemplateSelectionService.SelectForTenantInvoiceAsync"/>
    /// for the full contract.
    /// </summary>
    private async Task<InvoiceTemplate?> SelectForScopeAsync(
        Guid? tenantId, Guid? explicitTemplateId, CancellationToken ct)
    {
        if (explicitTemplateId is null)
        {
            // Default-fallback path. Repo's GetDefaultInScopeAsync
            // already filters to Active templates so callers never
            // see a stale Draft / Retired default.
            return await _repository.GetDefaultInScopeAsync(tenantId, ct);
        }

        var id = explicitTemplateId.Value;
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Explicit invoice template id must be a non-empty GUID.",
                nameof(explicitTemplateId));

        // Read-only scoped lookup: cross-scope ids surface as null
        // here (no existence leak). We immediately translate that
        // null into a typed exception because the caller asked for a
        // specific id and getting silently no template back would
        // mask the failure.
        var template = await _repository.GetByIdInScopeReadOnlyAsync(tenantId, id, ct);
        if (template is null)
            throw new InvoiceTemplateNotFoundInScopeException(id);

        // Lifecycle gate: only Active templates may be stamped onto
        // a new invoice. Draft is "still being authored" and Retired
        // is "soft-removed"; both would corrupt the invoice's
        // appearance if allowed through.
        if (template.Status != InvoiceTemplateStatus.Active)
            throw new InvoiceTemplateNotSelectableException(id, template.Status);

        return template;
    }

    // -----------------------------------------------------------------
    // Create
    // -----------------------------------------------------------------

    public async Task<InvoiceTemplate> CreateAsync(Guid? tenantId, NewInvoiceTemplate input, CancellationToken ct = default)
    {
        ValidateTenantScopeShape(tenantId);
        ArgumentNullException.ThrowIfNull(input);

        var ownerType = tenantId is null
            ? InvoiceTemplateOwnerType.Platform
            : InvoiceTemplateOwnerType.Tenant;

        var status = string.IsNullOrWhiteSpace(input.Status)
            ? InvoiceTemplateStatus.Draft
            : InvoiceTemplateValidation.ValidateStatus(input.Status);

        var template = new InvoiceTemplate
        {
            Id = Guid.NewGuid(),
            OwnerType = ownerType,
            BillingAccountId = tenantId,
            BillingProfileId = null,
            Name = InvoiceTemplateValidation.NormalizeName(input.Name),
            Description = InvoiceTemplateValidation.NormalizeOptionalText(
                input.Description, InvoiceTemplateValidation.DescriptionMaxLength, nameof(input.Description)),
            Status = status,
            // IsDefault = false here unconditionally; we promote below
            // either via the auto-default rule or via an explicit
            // IsDefault=true request. Doing it in one place avoids two
            // sources of truth for the unique-default invariant.
            IsDefault = false,
            LogoUrl = InvoiceTemplateValidation.NormalizeLogoUrl(input.LogoUrl),
            AccentColor = InvoiceTemplateValidation.NormalizeAccentColor(input.AccentColor),
            HeaderText = InvoiceTemplateValidation.NormalizeOptionalText(
                input.HeaderText, InvoiceTemplateValidation.HeaderTextMaxLength, nameof(input.HeaderText)),
            FooterText = InvoiceTemplateValidation.NormalizeOptionalText(
                input.FooterText, InvoiceTemplateValidation.FooterTextMaxLength, nameof(input.FooterText)),
            PaymentInstructions = InvoiceTemplateValidation.NormalizeOptionalText(
                input.PaymentInstructions, InvoiceTemplateValidation.PaymentInstructionsMaxLength, nameof(input.PaymentInstructions)),
            TermsText = InvoiceTemplateValidation.NormalizeOptionalText(
                input.TermsText, InvoiceTemplateValidation.TermsTextMaxLength, nameof(input.TermsText)),
            MemoPlaceholder = InvoiceTemplateValidation.NormalizeOptionalText(
                input.MemoPlaceholder, InvoiceTemplateValidation.MemoPlaceholderMaxLength, nameof(input.MemoPlaceholder)),
            DefaultDueDays = InvoiceTemplateValidation.ValidateDefaultDueDays(input.DefaultDueDays),
            InvoiceNumberPrefix = InvoiceTemplateValidation.NormalizeInvoiceNumberPrefix(input.InvoiceNumberPrefix),
            InvoiceNumberFormat = InvoiceTemplateValidation.NormalizeInvoiceNumberFormat(input.InvoiceNumberFormat),
            DisplayBillingAddress = input.DisplayBillingAddress ?? true,
            DisplayPaymentInstructions = input.DisplayPaymentInstructions ?? true,
            DisplayTerms = input.DisplayTerms ?? true,

            // INV-TPL-04: issuer / seller identity. Each value is
            // trimmed + length-checked (and email/website also
            // shape-checked). Blank inputs collapse to null so a
            // template carries either a real value or no value.
            IssuerDisplayName = InvoiceTemplateValidation.NormalizeOptionalText(
                input.IssuerDisplayName, InvoiceTemplateValidation.IssuerDisplayNameMaxLength, nameof(input.IssuerDisplayName)),
            IssuerLegalName = InvoiceTemplateValidation.NormalizeOptionalText(
                input.IssuerLegalName, InvoiceTemplateValidation.IssuerLegalNameMaxLength, nameof(input.IssuerLegalName)),
            IssuerAddressLine1 = InvoiceTemplateValidation.NormalizeOptionalText(
                input.IssuerAddressLine1, InvoiceTemplateValidation.IssuerAddressLineMaxLength, nameof(input.IssuerAddressLine1)),
            IssuerAddressLine2 = InvoiceTemplateValidation.NormalizeOptionalText(
                input.IssuerAddressLine2, InvoiceTemplateValidation.IssuerAddressLineMaxLength, nameof(input.IssuerAddressLine2)),
            IssuerCity = InvoiceTemplateValidation.NormalizeOptionalText(
                input.IssuerCity, InvoiceTemplateValidation.IssuerCityMaxLength, nameof(input.IssuerCity)),
            IssuerStateRegion = InvoiceTemplateValidation.NormalizeOptionalText(
                input.IssuerStateRegion, InvoiceTemplateValidation.IssuerStateRegionMaxLength, nameof(input.IssuerStateRegion)),
            IssuerPostalCode = InvoiceTemplateValidation.NormalizeOptionalText(
                input.IssuerPostalCode, InvoiceTemplateValidation.IssuerPostalCodeMaxLength, nameof(input.IssuerPostalCode)),
            IssuerCountry = InvoiceTemplateValidation.NormalizeOptionalText(
                input.IssuerCountry, InvoiceTemplateValidation.IssuerCountryMaxLength, nameof(input.IssuerCountry)),
            IssuerEmail = InvoiceTemplateValidation.NormalizeIssuerEmail(input.IssuerEmail),
            IssuerPhone = InvoiceTemplateValidation.NormalizeOptionalText(
                input.IssuerPhone, InvoiceTemplateValidation.IssuerPhoneMaxLength, nameof(input.IssuerPhone)),
            IssuerTaxId = InvoiceTemplateValidation.NormalizeOptionalText(
                input.IssuerTaxId, InvoiceTemplateValidation.IssuerTaxIdMaxLength, nameof(input.IssuerTaxId)),
            IssuerWebsite = InvoiceTemplateValidation.NormalizeIssuerWebsite(input.IssuerWebsite),

            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        // Determine whether this template should land as the scope's
        // default. Two paths:
        //   1. Caller asked explicitly via IsDefault=true → must be
        //      Active (Draft can't be selectable so can't be default).
        //   2. Caller did not ask, but Status=Active and the scope has
        //      no default yet → auto-default per spec.
        var explicitDefault = input.IsDefault == true;
        var autoDefault = !explicitDefault
            && status == InvoiceTemplateStatus.Active
            && !await _repository.AnyDefaultInScopeAsync(tenantId, ct);

        if (explicitDefault && status != InvoiceTemplateStatus.Active)
            throw new InvalidInvoiceTemplateStatusTransitionException(status, "Default")
            {
                // No data to attach — keep the FromStatus/ToStatus
                // semantics since this is a state-rule violation.
            };

        if (explicitDefault || autoDefault)
        {
            // Same atomic-default flow as MakeDefaultAsync — single
            // transaction unset-others + set-self so the unique-default
            // invariant cannot transiently break.
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
    // Update / lifecycle transitions
    // -----------------------------------------------------------------

    public async Task<InvoiceTemplate?> UpdateAsync(Guid? tenantId, Guid id, InvoiceTemplateUpdate update, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Template id is required.", nameof(id));
        ValidateTenantScopeShape(tenantId);
        ArgumentNullException.ThrowIfNull(update);

        var existing = await _repository.GetByIdInScopeAsync(tenantId, id, ct);
        if (existing is null) return null;

        if (existing.Status == InvoiceTemplateStatus.Retired)
            throw new InvalidInvoiceTemplateStatusTransitionException(existing.Status, "Edited");

        if (update.Name is not null)
            existing.Name = InvoiceTemplateValidation.NormalizeName(update.Name);
        if (update.Description is not null)
            existing.Description = InvoiceTemplateValidation.NormalizeOptionalText(
                update.Description, InvoiceTemplateValidation.DescriptionMaxLength, nameof(update.Description));
        if (update.LogoUrl is not null)
            existing.LogoUrl = InvoiceTemplateValidation.NormalizeLogoUrl(update.LogoUrl);
        if (update.AccentColor is not null)
            existing.AccentColor = InvoiceTemplateValidation.NormalizeAccentColor(update.AccentColor);
        if (update.HeaderText is not null)
            existing.HeaderText = InvoiceTemplateValidation.NormalizeOptionalText(
                update.HeaderText, InvoiceTemplateValidation.HeaderTextMaxLength, nameof(update.HeaderText));
        if (update.FooterText is not null)
            existing.FooterText = InvoiceTemplateValidation.NormalizeOptionalText(
                update.FooterText, InvoiceTemplateValidation.FooterTextMaxLength, nameof(update.FooterText));
        if (update.PaymentInstructions is not null)
            existing.PaymentInstructions = InvoiceTemplateValidation.NormalizeOptionalText(
                update.PaymentInstructions, InvoiceTemplateValidation.PaymentInstructionsMaxLength, nameof(update.PaymentInstructions));
        if (update.TermsText is not null)
            existing.TermsText = InvoiceTemplateValidation.NormalizeOptionalText(
                update.TermsText, InvoiceTemplateValidation.TermsTextMaxLength, nameof(update.TermsText));
        if (update.MemoPlaceholder is not null)
            existing.MemoPlaceholder = InvoiceTemplateValidation.NormalizeOptionalText(
                update.MemoPlaceholder, InvoiceTemplateValidation.MemoPlaceholderMaxLength, nameof(update.MemoPlaceholder));
        if (update.DefaultDueDays is not null)
            existing.DefaultDueDays = InvoiceTemplateValidation.ValidateDefaultDueDays(update.DefaultDueDays);
        if (update.InvoiceNumberPrefix is not null)
            existing.InvoiceNumberPrefix = InvoiceTemplateValidation.NormalizeInvoiceNumberPrefix(update.InvoiceNumberPrefix);
        if (update.InvoiceNumberFormat is not null)
            existing.InvoiceNumberFormat = InvoiceTemplateValidation.NormalizeInvoiceNumberFormat(update.InvoiceNumberFormat);
        if (update.DisplayBillingAddress is not null)
            existing.DisplayBillingAddress = update.DisplayBillingAddress.Value;
        if (update.DisplayPaymentInstructions is not null)
            existing.DisplayPaymentInstructions = update.DisplayPaymentInstructions.Value;
        if (update.DisplayTerms is not null)
            existing.DisplayTerms = update.DisplayTerms.Value;

        // INV-TPL-04: issuer field updates. Same null=no-change /
        // value=replace contract as the rest of this method. The
        // normalizers collapse a trimmed-empty value to null so an
        // operator can clear a field by sending "" deliberately.
        if (update.IssuerDisplayName is not null)
            existing.IssuerDisplayName = InvoiceTemplateValidation.NormalizeOptionalText(
                update.IssuerDisplayName, InvoiceTemplateValidation.IssuerDisplayNameMaxLength, nameof(update.IssuerDisplayName));
        if (update.IssuerLegalName is not null)
            existing.IssuerLegalName = InvoiceTemplateValidation.NormalizeOptionalText(
                update.IssuerLegalName, InvoiceTemplateValidation.IssuerLegalNameMaxLength, nameof(update.IssuerLegalName));
        if (update.IssuerAddressLine1 is not null)
            existing.IssuerAddressLine1 = InvoiceTemplateValidation.NormalizeOptionalText(
                update.IssuerAddressLine1, InvoiceTemplateValidation.IssuerAddressLineMaxLength, nameof(update.IssuerAddressLine1));
        if (update.IssuerAddressLine2 is not null)
            existing.IssuerAddressLine2 = InvoiceTemplateValidation.NormalizeOptionalText(
                update.IssuerAddressLine2, InvoiceTemplateValidation.IssuerAddressLineMaxLength, nameof(update.IssuerAddressLine2));
        if (update.IssuerCity is not null)
            existing.IssuerCity = InvoiceTemplateValidation.NormalizeOptionalText(
                update.IssuerCity, InvoiceTemplateValidation.IssuerCityMaxLength, nameof(update.IssuerCity));
        if (update.IssuerStateRegion is not null)
            existing.IssuerStateRegion = InvoiceTemplateValidation.NormalizeOptionalText(
                update.IssuerStateRegion, InvoiceTemplateValidation.IssuerStateRegionMaxLength, nameof(update.IssuerStateRegion));
        if (update.IssuerPostalCode is not null)
            existing.IssuerPostalCode = InvoiceTemplateValidation.NormalizeOptionalText(
                update.IssuerPostalCode, InvoiceTemplateValidation.IssuerPostalCodeMaxLength, nameof(update.IssuerPostalCode));
        if (update.IssuerCountry is not null)
            existing.IssuerCountry = InvoiceTemplateValidation.NormalizeOptionalText(
                update.IssuerCountry, InvoiceTemplateValidation.IssuerCountryMaxLength, nameof(update.IssuerCountry));
        if (update.IssuerEmail is not null)
            existing.IssuerEmail = InvoiceTemplateValidation.NormalizeIssuerEmail(update.IssuerEmail);
        if (update.IssuerPhone is not null)
            existing.IssuerPhone = InvoiceTemplateValidation.NormalizeOptionalText(
                update.IssuerPhone, InvoiceTemplateValidation.IssuerPhoneMaxLength, nameof(update.IssuerPhone));
        if (update.IssuerTaxId is not null)
            existing.IssuerTaxId = InvoiceTemplateValidation.NormalizeOptionalText(
                update.IssuerTaxId, InvoiceTemplateValidation.IssuerTaxIdMaxLength, nameof(update.IssuerTaxId));
        if (update.IssuerWebsite is not null)
            existing.IssuerWebsite = InvoiceTemplateValidation.NormalizeIssuerWebsite(update.IssuerWebsite);

        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(existing, ct);
        return existing;
    }

    public async Task<InvoiceTemplate?> ActivateAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Template id is required.", nameof(id));
        ValidateTenantScopeShape(tenantId);

        var existing = await _repository.GetByIdInScopeAsync(tenantId, id, ct);
        if (existing is null) return null;

        // Allowed transitions to Active:
        //   Draft  -> Active (normal publish)
        //   Active -> Active (idempotent no-op so callers can retry)
        // Disallowed: Retired -> Active (operator must clone a new
        // template instead; resurrecting retired templates would let
        // a stale brand re-enter circulation silently).
        if (existing.Status == InvoiceTemplateStatus.Retired)
            throw new InvalidInvoiceTemplateStatusTransitionException(existing.Status, InvoiceTemplateStatus.Active);

        if (existing.Status == InvoiceTemplateStatus.Active)
            return existing;

        existing.Status = InvoiceTemplateStatus.Active;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(existing, ct);
        return existing;
    }

    public async Task<InvoiceTemplate?> RetireAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Template id is required.", nameof(id));
        ValidateTenantScopeShape(tenantId);

        var existing = await _repository.GetByIdInScopeAsync(tenantId, id, ct);
        if (existing is null) return null;

        if (existing.Status == InvoiceTemplateStatus.Retired)
            return existing; // idempotent

        existing.Status = InvoiceTemplateStatus.Retired;
        // Retiring the current default also clears the default flag —
        // the spec forbids retired templates from being default. The
        // scope is now defaultless until an operator picks a new one.
        existing.IsDefault = false;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(existing, ct);
        return existing;
    }

    public async Task<InvoiceTemplate?> MakeDefaultAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Template id is required.", nameof(id));
        ValidateTenantScopeShape(tenantId);

        var existing = await _repository.GetByIdInScopeAsync(tenantId, id, ct);
        if (existing is null) return null;

        if (existing.Status == InvoiceTemplateStatus.Retired)
            throw new RetiredInvoiceTemplateCannotBeDefaultException(id);

        if (existing.Status == InvoiceTemplateStatus.Draft)
            throw new InvalidInvoiceTemplateStatusTransitionException(existing.Status, "Default");

        if (existing.IsDefault)
            return existing; // idempotent

        var now = DateTime.UtcNow;

        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
        // Order matters: unset peers FIRST, then set self. Reversing
        // the order would briefly create two defaults in the scope and
        // a concurrent reader could observe both.
        await _repository.UnsetDefaultsInScopeAsync(tenantId, id, now, ct);
        existing.IsDefault = true;
        existing.UpdatedAtUtc = now;
        await _repository.UpdateAsync(existing, ct);
        await tx.CommitAsync(ct);
        return existing;
    }

    private static void ValidateTenantScopeShape(Guid? tenantId)
    {
        if (tenantId is { } id && id == Guid.Empty)
            throw new ArgumentException("TenantId must be a non-empty GUID for tenant-scoped operations.", nameof(tenantId));
    }
}
