using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Services;

/// <summary>
/// INV-TPL-02: copies the relevant branding fields from an
/// <see cref="InvoiceTemplate"/> onto an <see cref="Invoice"/> in
/// memory. The act of stamping is the entire mechanism by which an
/// invoice's appearance becomes immutable against later edits to its
/// template — once the snapshot lives on the invoice row, mutations
/// to the template never propagate back.
///
/// The service is pure (no I/O of its own) so it can be unit-tested
/// in isolation. Persistence happens in two distinct flows:
/// <list type="bullet">
///   <item>Create path: <see cref="StampInvoice"/> mutates the
///     in-memory <see cref="Invoice"/> before
///     <c>IInvoiceRepository.AddAsync</c>, so the full row is
///     written in a single insert.</item>
///   <item>Issue path (ensure-stamp): the service is invoked via
///     <c>IInvoiceRepository.ApplyStampAsync</c>, which loads a
///     tracked instance, calls
///     <see cref="EnsureStampedInvoice"/>, then saves. The
///     "ensure" wrapper short-circuits when the invoice already
///     carries a snapshot.</item>
/// </list>
///
/// INV-TPL-04: the stamp now also covers the issuer / seller
/// identity block. Issuer fields move atomically with the branding
/// snapshot — there is no path that writes branding without issuer
/// or vice versa, so the existing idempotency guard on
/// <c>InvoiceTemplateId</c> protects both.
/// </summary>
public interface IInvoiceTemplateStampingService
{
    /// <summary>
    /// Unconditionally copy the template's branding-snapshot fields
    /// onto the invoice and stamp <c>TemplateStampedAtUtc</c> with
    /// <paramref name="nowUtc"/>. Used by the create path where the
    /// invoice is brand-new and any pre-existing snapshot would be
    /// the result of a programming error rather than legitimate
    /// data.
    /// </summary>
    void StampInvoice(Invoice invoice, InvoiceTemplate template, DateTime nowUtc);

    /// <summary>
    /// Stamp the invoice only if it does not already carry a
    /// snapshot (i.e. <c>InvoiceTemplateId</c> is null). Returns
    /// <c>true</c> when a stamp was applied, <c>false</c> when the
    /// invoice was already stamped.
    ///
    /// This is the issue-path entry point: a Draft invoice that
    /// missed the create-path stamp (e.g. the tenant had no default
    /// at create time but later configured one) gets stamped on its
    /// way to Issued. An already-stamped Draft remains untouched —
    /// silently re-stamping would defeat the snapshot guarantee.
    /// </summary>
    bool EnsureStampedInvoice(Invoice invoice, InvoiceTemplate template, DateTime nowUtc);
}

/// <inheritdoc cref="IInvoiceTemplateStampingService"/>
public sealed class InvoiceTemplateStampingService : IInvoiceTemplateStampingService
{
    public void StampInvoice(Invoice invoice, InvoiceTemplate template, DateTime nowUtc)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));
        if (template is null) throw new ArgumentNullException(nameof(template));

        // Identity + provenance. We snapshot OwnerType (not just the
        // id) so a future "list invoices stamped from platform
        // templates" query has a fast filter and so a deleted-
        // template scenario still surfaces the original scope.
        invoice.InvoiceTemplateId = template.Id;
        invoice.TemplateOwnerType = template.OwnerType;
        invoice.TemplateName = template.Name;

        // Branding (visual). Nullable on the template ⇒ nullable on
        // the invoice — we copy nulls through deliberately, because
        // "this template intentionally has no logo" is meaningful
        // information that downstream renderers need to honour.
        invoice.TemplateLogoUrl = template.LogoUrl;
        invoice.TemplateAccentColor = template.AccentColor;
        invoice.TemplateHeaderText = template.HeaderText;
        invoice.TemplateFooterText = template.FooterText;

        // Payment / terms / memo. Copied verbatim — the snapshot
        // is the source of truth for what *this* invoice should
        // display, regardless of any later template edit.
        invoice.TemplatePaymentInstructions = template.PaymentInstructions;
        invoice.TemplateTermsText = template.TermsText;
        invoice.TemplateMemoPlaceholder = template.MemoPlaceholder;

        // Display toggles. Stored as non-nullable bools so absence
        // of a stamp ⇒ false (renderer hides the section). When
        // stamped, take the template's choice exactly.
        invoice.TemplateDisplayBillingAddress = template.DisplayBillingAddress;
        invoice.TemplateDisplayPaymentInstructions = template.DisplayPaymentInstructions;
        invoice.TemplateDisplayTerms = template.DisplayTerms;

        invoice.TemplateStampedAtUtc = nowUtc;

        // ---- INV-TPL-04: issuer / seller identity snapshot ----
        //
        // Same rules as the branding block: copy nulls verbatim,
        // do not invent any field the template itself did not have.
        // We always set IssuerStampedAtUtc when this method runs,
        // even if every issuer field on the template is null —
        // "stamped at T with no issuer info" is still semantically
        // distinct from "never stamped" and is what allows the
        // renderer to reason about coverage cleanly.
        invoice.IssuerDisplayName = template.IssuerDisplayName;
        invoice.IssuerLegalName = template.IssuerLegalName;
        invoice.IssuerAddressLine1 = template.IssuerAddressLine1;
        invoice.IssuerAddressLine2 = template.IssuerAddressLine2;
        invoice.IssuerCity = template.IssuerCity;
        invoice.IssuerStateRegion = template.IssuerStateRegion;
        invoice.IssuerPostalCode = template.IssuerPostalCode;
        invoice.IssuerCountry = template.IssuerCountry;
        invoice.IssuerEmail = template.IssuerEmail;
        invoice.IssuerPhone = template.IssuerPhone;
        invoice.IssuerTaxId = template.IssuerTaxId;
        invoice.IssuerWebsite = template.IssuerWebsite;
        invoice.IssuerStampedAtUtc = nowUtc;
    }

    public bool EnsureStampedInvoice(Invoice invoice, InvoiceTemplate template, DateTime nowUtc)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));

        // Idempotency guard: an already-snapshotted invoice keeps
        // its original branding (and its original issuer block). The
        // gate is on InvoiceTemplateId because branding + issuer
        // always move together — if template id is set we trust the
        // companion fields are also set to whatever the template
        // had at the moment of stamping.
        if (invoice.InvoiceTemplateId.HasValue) return false;

        StampInvoice(invoice, template, nowUtc);
        return true;
    }
}
