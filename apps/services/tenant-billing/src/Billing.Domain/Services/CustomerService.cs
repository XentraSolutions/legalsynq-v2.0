using System.Globalization;
using System.Text.RegularExpressions;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.Services;

public sealed class CustomerService : ICustomerService
{
    // Pragmatic email-shape check; the API layer also enforces [EmailAddress]
    // on inbound DTOs so this is defense-in-depth for non-HTTP callers.
    private static readonly Regex EmailShape = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // ---- INV-TPL-04: structured billing address bounds ----
    //
    // Sized to comfortably hold international addresses without
    // bloating the customers table. Kept here (rather than in
    // InvoiceTemplateValidation) because customers are authored by a
    // different surface than templates.
    private const int AddressLineMaxLength = 250;
    private const int CityMaxLength = 100;
    private const int StateRegionMaxLength = 100;
    private const int PostalCodeMaxLength = 100;
    private const int CountryMaxLength = 100;

    private readonly ICustomerRepository _repository;

    public CustomerService(ICustomerRepository repository)
    {
        _repository = repository;
    }

    public async Task<Customer> CreateAsync(
        Guid tenantId,
        string name,
        string email,
        string? phone,
        string? billingAddress,
        string? externalReference,
        string? notes,
        CustomerBillingAddress? billingAddressDetails = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        var normalizedName = ValidateAndTrimName(name);
        var normalizedEmail = ValidateAndNormalizeEmail(email);

        if (await _repository.ExistsByTenantAndEmailAsync(tenantId, normalizedEmail, excludingCustomerId: null, ct))
            throw new DuplicateCustomerEmailException(tenantId, normalizedEmail);

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName,
            Email = normalizedEmail,
            Phone = NullIfBlank(phone),
            BillingAddress = NullIfBlank(billingAddress),
            ExternalReference = NullIfBlank(externalReference),
            Notes = NullIfBlank(notes),
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        ApplyBillingAddressDetails(customer, billingAddressDetails);

        return await _repository.AddAsync(customer, ct);
    }

    public async Task<Customer?> UpdateAsync(
        Guid tenantId,
        Guid customerId,
        string name,
        string email,
        string? phone,
        string? billingAddress,
        string? externalReference,
        string? notes,
        CustomerBillingAddress? billingAddressDetails = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId is required.", nameof(customerId));

        // GetActiveByIdAsync returns null for soft-deleted or wrong-tenant rows,
        // collapsing both "not found" and "tenant mismatch" into a single 404.
        var existing = await _repository.GetActiveByIdAsync(tenantId, customerId, ct);
        if (existing is null) return null;

        var normalizedName = ValidateAndTrimName(name);
        var normalizedEmail = ValidateAndNormalizeEmail(email);

        if (await _repository.ExistsByTenantAndEmailAsync(tenantId, normalizedEmail, excludingCustomerId: customerId, ct))
            throw new DuplicateCustomerEmailException(tenantId, normalizedEmail);

        existing.Name = normalizedName;
        existing.Email = normalizedEmail;
        existing.Phone = NullIfBlank(phone);
        existing.BillingAddress = NullIfBlank(billingAddress);
        existing.ExternalReference = NullIfBlank(externalReference);
        existing.Notes = NullIfBlank(notes);
        existing.UpdatedAt = DateTime.UtcNow;
        // CreatedAt and IsDeleted are intentionally not modified by Update.

        ApplyBillingAddressDetails(existing, billingAddressDetails);

        return await _repository.UpdateAsync(existing, ct);
    }

    public Task<Customer?> GetAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId is required.", nameof(customerId));

        return _repository.GetActiveByIdAsync(tenantId, customerId, ct);
    }

    public async Task<CustomerPage> ListAsync(
        Guid tenantId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        var (effectivePage, effectivePageSize) = NormalizePaging(page, pageSize);
        var trimmedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var totalCount = await _repository.CountAsync(tenantId, trimmedSearch, ct);
        var items = await _repository.ListAsync(tenantId, trimmedSearch, effectivePage, effectivePageSize, ct);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)effectivePageSize);

        return new CustomerPage(items, effectivePage, effectivePageSize, totalCount, totalPages);
    }

    public async Task<CustomerByExternalReferenceResult> GetByExternalReferenceAsync(
        Guid tenantId,
        string externalReference,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        // A blank/whitespace needle can never match (the service stores
        // blank externalReference as null). Map directly to NotFound so
        // the controller surfaces a uniform 404 without the repository
        // even being consulted.
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return new CustomerByExternalReferenceResult(CustomerLookupOutcome.NotFound, null);
        }

        // Ask the repo for up to 2 matches: that is the minimum required
        // to distinguish unique vs ambiguous without paginating the entire
        // tenant's customer list.
        var matches = await _repository.GetByExternalReferenceAsync(
            tenantId,
            externalReference,
            limit: 2,
            ct);

        return matches.Count switch
        {
            0 => new CustomerByExternalReferenceResult(CustomerLookupOutcome.NotFound, null),
            1 => new CustomerByExternalReferenceResult(CustomerLookupOutcome.Found, matches[0]),
            _ => new CustomerByExternalReferenceResult(CustomerLookupOutcome.Ambiguous, null),
        };
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId is required.", nameof(customerId));

        var existing = await _repository.GetActiveByIdAsync(tenantId, customerId, ct);
        if (existing is null) return false;

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(existing, ct);
        return true;
    }

    // ----------- helpers -----------

    private static string ValidateAndTrimName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        return name.Trim();
    }

    private static string ValidateAndNormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        var normalized = email.Trim().ToLower(CultureInfo.InvariantCulture);
        if (!EmailShape.IsMatch(normalized))
            throw new ArgumentException("Email is not a valid email address.", nameof(email));

        return normalized;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// INV-TPL-04 — copy + length-check the structured address bundle
    /// onto the customer. A null bundle leaves existing values
    /// untouched (so older positional callers still work). A non-null
    /// bundle replaces every column individually, treating blank as
    /// null so callers can clear a field by sending "".
    /// </summary>
    private static void ApplyBillingAddressDetails(Customer customer, CustomerBillingAddress? details)
    {
        if (details is null) return;

        customer.BillingAddressLine1 = NormalizeAddressField(details.Line1, AddressLineMaxLength, nameof(details.Line1));
        customer.BillingAddressLine2 = NormalizeAddressField(details.Line2, AddressLineMaxLength, nameof(details.Line2));
        customer.BillingCity = NormalizeAddressField(details.City, CityMaxLength, nameof(details.City));
        customer.BillingStateRegion = NormalizeAddressField(details.StateRegion, StateRegionMaxLength, nameof(details.StateRegion));
        customer.BillingPostalCode = NormalizeAddressField(details.PostalCode, PostalCodeMaxLength, nameof(details.PostalCode));
        customer.BillingCountry = NormalizeAddressField(details.Country, CountryMaxLength, nameof(details.Country));
    }

    private static string? NormalizeAddressField(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"{fieldName} must be at most {maxLength} characters.", fieldName);
        return trimmed;
    }

    private static (int page, int pageSize) NormalizePaging(int page, int pageSize)
    {
        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize < 1
            ? ICustomerService.DefaultPageSize
            : (pageSize > ICustomerService.MaxPageSize ? ICustomerService.MaxPageSize : pageSize);
        return (effectivePage, effectivePageSize);
    }
}
