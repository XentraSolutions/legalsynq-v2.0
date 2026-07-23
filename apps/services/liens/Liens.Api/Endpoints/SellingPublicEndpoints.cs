using System.Globalization;
using System.Net;
using System.Text;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Api.Endpoints;

public static class SellingPublicEndpoints
{
    private static readonly string[] DocumentTaskTypes =
    [
        "LegacyCaseDocument",
        "LegacyLienDocument",
        "LegacyMedicalDocument",
    ];

    public static void MapSellingPublicEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/selling/public")
            .AllowAnonymous();

        group.MapGet("/{token}", GetTemporaryBuyerPortal)
            .AllowAnonymous();
    }

    private static async Task<IResult> GetTemporaryBuyerPortal(
        string token,
        LiensDbContext db,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return HtmlResult(
                RenderLinkStatePage(httpContext, "Lien offer link unavailable", "The secure link is missing from this request."),
                StatusCodes.Status404NotFound);
        }

        var accessLink = await db.SellingBuyerAccessLinks
            .FirstOrDefaultAsync(link => link.Token == token.Trim(), ct);

        if (accessLink is null)
        {
            return HtmlResult(
                RenderLinkStatePage(httpContext, "Lien offer link unavailable", "The secure link could not be found."),
                StatusCodes.Status404NotFound);
        }

        if (accessLink.RevokedAtUtc.HasValue)
        {
            return HtmlResult(
                RenderLinkStatePage(httpContext, "Lien offer link revoked", "This secure link is no longer active."),
                StatusCodes.Status410Gone);
        }

        if (accessLink.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return HtmlResult(
                RenderLinkStatePage(httpContext, "Lien offer link expired", "This secure link has expired."),
                StatusCodes.Status410Gone);
        }

        var view = await BuildPublicViewAsync(db, accessLink, ct);
        if (view is null)
        {
            return HtmlResult(
                RenderLinkStatePage(httpContext, "Lien offer unavailable", "The lien offer data could not be resolved."),
                StatusCodes.Status404NotFound);
        }

        accessLink.MarkAccessed();
        await db.SaveChangesAsync(ct);

        return HtmlResult(RenderPortalPage(httpContext, view), StatusCodes.Status200OK);
    }

    private static async Task<PublicPortalView?> BuildPublicViewAsync(
        LiensDbContext db,
        SellingBuyerAccessLink accessLink,
        CancellationToken ct)
    {
        var lien = await db.Liens
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == accessLink.TenantId && l.Id == accessLink.LienId, ct);

        if (lien is null)
            return null;

        var caseEntity = lien.CaseId.HasValue
            ? await db.Cases
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == accessLink.TenantId && c.Id == lien.CaseId.Value, ct)
            : null;

        var buyerContact = await db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.TenantId == accessLink.TenantId &&
                c.Id == accessLink.BuyerContactId &&
                c.OrgId == accessLink.BuyerOrgId,
                ct);

        var sellerContacts = await db.Contacts
            .AsNoTracking()
            .Where(c => c.TenantId == accessLink.TenantId && c.OrgId == accessLink.SellerOrgId && c.IsActive)
            .ToListAsync(ct);

        var sellerContact = SelectSellerContact(sellerContacts);
        var handlingLawFirm = await ResolveHandlingLawFirmAsync(db, accessLink.TenantId, caseEntity, ct);
        var caseManager = await ResolveCaseManagerAsync(db, accessLink.TenantId, caseEntity, ct);
        var documents = await ResolveDocumentsAsync(db, accessLink.TenantId, lien, caseEntity, ct);

        return new PublicPortalView(
            accessLink,
            lien,
            caseEntity,
            buyerContact,
            sellerContact,
            handlingLawFirm,
            caseManager,
            documents);
    }

    private static async Task<string?> ResolveHandlingLawFirmAsync(
        LiensDbContext db,
        Guid tenantId,
        Case? caseEntity,
        CancellationToken ct)
    {
        if (caseEntity is null)
            return null;

        var metadata = ParseLegacyNoteFields(caseEntity.Notes);
        if (Guid.TryParse(metadata.GetValueOrDefault("lawFirmId"), out var lawFirmId))
        {
            var lawFirm = await db.Contacts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == lawFirmId, ct);
            var name = FirstNonEmpty(lawFirm?.Organization, lawFirm?.DisplayName);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        var defaultLawFirm = await db.Contacts
            .AsNoTracking()
            .Where(c =>
                c.TenantId == tenantId &&
                c.OrgId == caseEntity.OrgId &&
                c.IsActive &&
                c.ContactType == ContactType.LawFirm &&
                c.ContactSubtype == null)
            .OrderBy(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        return FirstNonEmpty(defaultLawFirm?.Organization, defaultLawFirm?.DisplayName);
    }

    private static async Task<string?> ResolveCaseManagerAsync(
        LiensDbContext db,
        Guid tenantId,
        Case? caseEntity,
        CancellationToken ct)
    {
        if (caseEntity is null)
            return null;

        var metadata = ParseLegacyNoteFields(caseEntity.Notes);
        if (!Guid.TryParse(metadata.GetValueOrDefault("caseManagerId"), out var caseManagerId))
            return null;

        var caseManager = await db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == caseManagerId, ct);

        return FirstNonEmpty(caseManager?.DisplayName);
    }

    private static async Task<IReadOnlyList<PublicDocumentView>> ResolveDocumentsAsync(
        LiensDbContext db,
        Guid tenantId,
        Lien lien,
        Case? caseEntity,
        CancellationToken ct)
    {
        var caseId = caseEntity?.Id;
        var query = db.ServicingItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

        query = caseId.HasValue
            ? query.Where(item => item.LienId == lien.Id || item.CaseId == caseId.Value)
            : query.Where(item => item.LienId == lien.Id);

        var items = await query
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(ct);

        return items
            .Where(item => DocumentTaskTypes.Contains(item.TaskType, StringComparer.Ordinal))
            .Select(MapDocument)
            .Where(document => !string.IsNullOrWhiteSpace(document.FileName))
            .DistinctBy(document => document.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static PublicDocumentView MapDocument(ServicingItem item)
    {
        var fields = ParseLegacyNoteFields(item.Notes);
        var fileName = FirstNonEmpty(
            fields.GetValueOrDefault("originalFileName"),
            fields.GetValueOrDefault("filename"),
            item.Description) ?? string.Empty;

        var category = FirstNonEmpty(
            fields.GetValueOrDefault("documentCategory"),
            fields.GetValueOrDefault("category"),
            HumanizeDocumentTaskType(item.TaskType));

        var size = FirstNonEmpty(
            fields.GetValueOrDefault("size"),
            fields.GetValueOrDefault("fileSize"),
            fields.GetValueOrDefault("contentLength"),
            ResolveFileExtension(fileName));

        return new PublicDocumentView(fileName.Trim(), category, FormatDocumentSize(size));
    }

    private static string RenderPortalPage(HttpContext httpContext, PublicPortalView view)
    {
        var sellerRows = new[]
        {
            new DataRow("Seller Name", view.SellerContact?.DisplayName),
            new DataRow("Seller Company", view.SellerContact?.Organization),
        };

        var lienRows = new[]
        {
            new DataRow("Submitted Date", FormatDateTime(view.Lien.SubmittedForSaleAtUtc ?? view.AccessLink.CreatedAtUtc), IsHtml: true),
            new DataRow("Listing Visibility", view.Lien.ListingVisibility),
            new DataRow("Initial Service Date", FormatDate(view.Lien.InitialServiceDate)),
            new DataRow("End Service Date", FormatDate(view.Lien.EndServiceDate)),
        };

        var fundingRows = new[]
        {
            new DataRow("Funding Company", view.BuyerContact?.Organization),
            new DataRow("Handling Law Firm", view.HandlingLawFirm),
            new DataRow("Contact Person", view.SellerContact?.DisplayName),
            new DataRow("Case Manager", view.CaseManager),
            new DataRow("Email Address", view.SellerContact?.Email),
        };

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("<title>")
            .Append(Html(ResolveLienCode(view.Lien)))
            .AppendLine(" - Lien Offer</title>");
        AppendStyles(html);
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<main class=\"portal-shell\">");
        AppendSafariToolbar(html, httpContext);
        AppendPortalHeader(html);
        html.AppendLine("<section class=\"portal-content\" aria-label=\"Temporary funding company portal\">");
        AppendHeroBanner(html);
        AppendResponseCard(html);
        html.AppendLine("<section class=\"card summary-card\" aria-labelledby=\"lien-summary-title\">");
        html.AppendLine("<div class=\"card-title-row\"><div class=\"card-title\">");
        AppendIcon(html, "chevron-down", "chevron");
        html.AppendLine("<h2 id=\"lien-summary-title\">Lien Summary</h2></div><span class=\"status-chip\">Awaiting Your Response</span></div>");
        AppendFieldSection(html, "Seller Information", sellerRows);
        AppendFieldSection(html, "Lien Information", lienRows, view.Lien);
        AppendFieldSection(html, "Funding Company &amp; Case Information", fundingRows);
        html.AppendLine("</section>");
        AppendDocumentsCard(html, view.Documents);
        AppendMessagesCard(html);
        html.AppendLine("<p class=\"secure-note\">Accessible only with the secure link from the email. The link will expire 30 days from the date it was sent.</p>");
        html.AppendLine("</section>");
        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static void AppendStyles(StringBuilder html)
    {
        html.AppendLine("""
<style>
:root{color-scheme:light;--navy:#0c1d33;--deep:#0d1e34;--orange:#ee7132;--orange-dark:#d85f25;--warning:#a16207;--text:#0a0a0a;--muted:#737373;--line:#e5e5e5;--surface:#ffffff;--surface-soft:#f5f5f5;--shadow:0 1px 1.5px rgba(0,0,0,.1);font-family:"Plus Jakarta Sans",Arial,"Helvetica Neue",sans-serif}
*{box-sizing:border-box}
body{margin:0;background:#f6f6f6;color:var(--text);font-family:"Plus Jakarta Sans",Arial,"Helvetica Neue",sans-serif}
button,input{font:inherit}
.portal-shell{width:100%;min-height:100vh;background:#fff;overflow:hidden}
.safari{height:53px;background:#fff;box-shadow:0 .5px 0 rgba(0,0,0,.15);display:grid;grid-template-columns:190px minmax(240px,600px) 1fr;align-items:center;padding:0 20px;gap:24px}
.traffic{display:flex;gap:8px;align-items:center}
.traffic span{width:12px;height:12px;border-radius:999px;display:inline-block}
.traffic span:nth-child(1){background:#ff5f57}.traffic span:nth-child(2){background:#febc2e}.traffic span:nth-child(3){background:#28c840}
.address{height:28px;border-radius:6px;background:rgba(0,0,0,.05);display:flex;align-items:center;justify-content:center;gap:6px;color:#4c4c4c;font:400 13px Arial,sans-serif;min-width:0}
.address span{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.toolbar-icons{justify-self:end;display:flex;gap:10px;color:#777;font-size:16px;letter-spacing:2px}
.brand-header{height:80px;background:var(--navy);display:flex;align-items:center;justify-content:center;padding:20px 24px}
.brand{display:flex;align-items:center;justify-content:center;width:137px;height:39.5px}
.brand-image{display:block;width:137px;height:39.5px;object-fit:contain}
.portal-content{display:flex;flex-direction:column;align-items:center;gap:24px;padding:24px 20px 34px;background:#fff}
.banner,.card{width:min(700px,100%);border-radius:16px}
.banner{position:relative;overflow:hidden;background:var(--deep);box-shadow:0 1px 3px rgba(0,0,0,.1);padding:32px;color:#fafafa}
.banner:before{content:"";position:absolute;inset:0;background:radial-gradient(circle at 86% 36%,rgba(255,255,255,.12),transparent 25%),linear-gradient(180deg,rgba(12,29,51,0),#0c1d33);opacity:.8}
.banner-logo{position:absolute;right:-54px;top:28px;width:176px;height:176px;background:url("/legalsynq-temp-portal-watermark.svg") center/contain no-repeat;opacity:1}
.banner-inner{position:relative;z-index:1}
.banner-top{display:flex;align-items:center;justify-content:space-between;gap:16px;margin-bottom:8px}
.banner h1,.card h2{margin:0;font-size:18px;line-height:1.6;font-weight:800;letter-spacing:0}
.banner p{margin:0;max-width:560px;color:rgba(250,250,250,.9);font-size:16px;line-height:1.6}
.button{height:38px;border-radius:10px;border:1px solid transparent;display:inline-flex;align-items:center;justify-content:center;padding:8px 16px;font-size:14px;font-weight:600;line-height:1.6;box-shadow:0 1px 2px rgba(0,0,0,.1);cursor:default;white-space:nowrap}
.button.primary{background:var(--orange);color:#fff}.button.primary:hover{background:var(--orange-dark)}
.button.danger{background:#fff;color:#dc2626;border-color:#dc2626}
.card{background:#fff;border:1px solid var(--line);box-shadow:var(--shadow);padding:24px}
.response-card{display:flex;flex-direction:column;gap:40px}
.text-holder{display:flex;flex-direction:column;gap:8px}
.text-holder p{margin:0;color:var(--muted);font-size:16px;line-height:1.6}
.action-buttons{display:flex;gap:12px}
.action-buttons .button{flex:1}
.response-actions{display:flex;flex-direction:column;gap:8px}
.response-note{margin:0;color:var(--muted);font-size:14px;line-height:1.6}.inline-link{background:transparent;border:0;color:var(--orange);cursor:default;padding:0;text-decoration:underline;text-underline-position:from-font}
.summary-card{display:flex;flex-direction:column;gap:24px;padding-bottom:8px}
.card-title-row{display:flex;align-items:center;justify-content:space-between;gap:16px}
.card-title{display:flex;align-items:center;gap:12px}
.icon{display:inline-flex;align-items:center;justify-content:center;color:currentColor;line-height:0}
.icon svg{display:block;width:100%;height:100%;stroke:currentColor}
.chevron{width:24px;height:24px;color:#0a0a0a}
.status-chip{height:28px;border-radius:999px;background:rgba(234,179,8,.15);color:var(--warning);display:inline-flex;align-items:center;justify-content:center;padding:4px 12px;font-size:14px;font-weight:600;line-height:1.6;white-space:nowrap}
.field-section{border-bottom:1px solid var(--line);padding-bottom:16px}
.field-section:last-child{border-bottom:0}
.section-label{display:flex;align-items:center;gap:8px;margin-bottom:16px;color:#0a0a0a;font-size:14px;font-weight:700;line-height:1.6}
.section-label .icon{width:18px;height:18px}
.field-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));column-gap:48px;row-gap:16px}
.field{display:flex;flex-direction:column;gap:6px;min-width:0}
.field label{font-size:14px;line-height:1.6;color:var(--muted)}
.field span{font-size:14px;line-height:1.6;color:#0a0a0a;font-weight:600;overflow-wrap:anywhere}
.field a{color:#0a0a0a;text-decoration:none}
.notes-block{display:flex;flex-direction:column;gap:6px;margin-top:16px}
.notes-block label{font-size:14px;color:var(--muted);line-height:1.6}
.notes-block p{margin:0;color:#0a0a0a;font-size:14px;line-height:1.6;font-weight:600}
.documents-card{display:flex;flex-direction:column;gap:24px;padding-bottom:24px}
.document-list{display:flex;flex-direction:column;gap:12px}
.document-row{border:1px dashed var(--line);border-radius:12px;padding:24px;display:flex;align-items:center;justify-content:space-between;gap:24px}
.document-left{display:flex;gap:12px;align-items:flex-start;min-width:0}
.avatar{width:56px;height:56px;flex:0 0 56px;border-radius:12px;background:var(--surface-soft);display:flex;align-items:center;justify-content:center;color:#333}
.avatar .icon{width:24px;height:24px}
.document-text{display:flex;flex-direction:column;gap:8px;min-width:0}
.document-name{font-size:16px;line-height:20px;color:#0a0a0a;font-weight:700;overflow-wrap:anywhere}
.document-meta{display:flex;align-items:center;gap:8px;color:var(--muted);font-size:16px;line-height:1.6;flex-wrap:wrap}
.document-actions{display:flex;gap:12px;align-items:center;flex:0 0 auto}
.icon-button{width:36px;height:36px;border-radius:10px;border:1px solid var(--line);background:#fff;box-shadow:0 1px 2px rgba(0,0,0,.1);display:flex;align-items:center;justify-content:center;color:#333}
.icon-button .icon{width:16px;height:16px}
.empty-state{display:flex;flex-direction:column;align-items:center;gap:16px;padding:40px 0;color:var(--muted);font-size:14px;line-height:1.6;text-align:center}
.message-input{border:1px solid var(--line);border-radius:12px;box-shadow:0 1px 2px rgba(0,0,0,.1);display:flex;align-items:center;gap:16px;padding:12px 12px 12px 16px;width:100%}
.message-input input{border:0;outline:0;flex:1;min-width:0;color:var(--muted);font-size:14px}
.counter{color:var(--muted);font-size:14px;white-space:nowrap}
.send{width:36px;height:36px;border-radius:999px;background:var(--orange);color:#fff;border:0;display:flex;align-items:center;justify-content:center;box-shadow:0 1px 2px rgba(0,0,0,.1)}
.send .icon{width:16px;height:16px}
.secure-note{margin:0;width:min(700px,100%);color:var(--muted);font-size:14px;line-height:1.6;text-align:center}
.state-page{min-height:100vh;background:#fff;display:flex;align-items:center;justify-content:center;padding:24px}.state-card{width:min(520px,100%);border:1px solid var(--line);border-radius:16px;padding:28px;box-shadow:var(--shadow);text-align:center}.state-card h1{font-size:22px;margin:0 0 8px}.state-card p{margin:0;color:var(--muted);line-height:1.6}
@media (max-width:760px){.safari{grid-template-columns:auto 1fr;gap:14px}.toolbar-icons{display:none}.brand-header{height:72px}.portal-content{padding:18px 14px 28px}.banner,.card{border-radius:14px}.banner{padding:24px}.banner-top{align-items:flex-start;flex-direction:column}.field-grid{grid-template-columns:1fr;gap:14px}.action-buttons,.document-row{flex-direction:column;align-items:stretch}.document-actions{align-self:flex-end}.status-chip{font-size:12px}.card-title-row{align-items:flex-start;flex-direction:column}}
</style>
""");
    }

    private static void AppendSafariToolbar(StringBuilder html, HttpContext httpContext)
    {
        var host = ResolveDisplayHost(httpContext);
        html.AppendLine("<div class=\"safari\" aria-hidden=\"true\">");
        html.AppendLine("<div class=\"traffic\"><span></span><span></span><span></span></div>");
        html.Append("<div class=\"address\"><span>&#128274;</span><span>")
            .Append(Html(host))
            .AppendLine("</span></div>");
        html.AppendLine("<div class=\"toolbar-icons\"><span>&#9675;</span><span>&#8599;</span><span>&#65291;</span></div>");
        html.AppendLine("</div>");
    }

    private static void AppendPortalHeader(StringBuilder html)
    {
        html.AppendLine("<header class=\"brand-header\">");
        AppendBrand(html);
        html.AppendLine("</header>");
    }

    private static void AppendBrand(StringBuilder html)
    {
        html.AppendLine("<div class=\"brand\" aria-label=\"LegalSynq\"><img class=\"brand-image\" src=\"/legalsynq-logo-temp-portal.svg\" alt=\"LegalSynq\" width=\"137\" height=\"40\"></div>");
    }

    private static void AppendHeroBanner(StringBuilder html)
    {
        html.AppendLine("<section class=\"banner\" aria-labelledby=\"manage-offered-liens-title\">");
        html.AppendLine("<div class=\"banner-logo\" aria-hidden=\"true\"></div>");
        html.AppendLine("<div class=\"banner-inner\">");
        html.AppendLine("<div class=\"banner-top\"><h1 id=\"manage-offered-liens-title\">Manage Offered Liens</h1><button class=\"button primary\" type=\"button\">Activate Free Account</button></div>");
        html.AppendLine("<p>Manage all lien submissions sent to your company, from initial review through the final purchase decision.</p>");
        html.AppendLine("</div>");
        html.AppendLine("</section>");
    }

    private static void AppendResponseCard(StringBuilder html)
    {
        html.AppendLine("<section class=\"card response-card\" aria-labelledby=\"response-title\">");
        html.AppendLine("<div class=\"text-holder\"><h2 id=\"response-title\">Your Response</h2><p>Respond directly from this page, or log in to your funding company dashboard.</p></div>");
        html.AppendLine("<div class=\"response-actions\"><div class=\"action-buttons\"><button class=\"button primary\" type=\"button\">Accept Lien</button><button class=\"button danger\" type=\"button\">Decline Lien</button></div><p class=\"response-note\">Your response is securely recorded. <button class=\"inline-link\" type=\"button\">Log in</button> to manage from your dashboard.</p></div>");
        html.AppendLine("</section>");
    }

    private static void AppendFieldSection(
        StringBuilder html,
        string title,
        IReadOnlyList<DataRow> rows,
        Lien? notesLien = null)
    {
        var visibleRows = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Value))
            .ToList();

        if (visibleRows.Count == 0 && notesLien is null)
            return;

        html.AppendLine("<section class=\"field-section\">");
        html.Append("<div class=\"section-label\">");
        AppendIcon(html, ResolveSectionIcon(title));
        html.Append("<span>")
            .Append(title)
            .AppendLine("</span></div>");
        if (visibleRows.Count > 0)
        {
            html.AppendLine("<div class=\"field-grid\">");
            foreach (var row in visibleRows)
            {
                html.Append("<div class=\"field\"><label>")
                    .Append(Html(row.Label))
                    .Append("</label><span>");
                AppendFieldValue(html, row);
                html.AppendLine("</span></div>");
            }
            html.AppendLine("</div>");
        }
        if (notesLien is not null)
            AppendLienNotes(html, notesLien);
        html.AppendLine("</section>");
    }

    private static void AppendFieldValue(StringBuilder html, DataRow row)
    {
        var value = row.Value!.Trim();
        if (string.Equals(row.Label, "Email Address", StringComparison.OrdinalIgnoreCase))
        {
            html.Append("<a href=\"mailto:")
                .Append(Html(value))
                .Append("\">")
                .Append(Html(value))
                .Append("</a>");
            return;
        }

        html.Append(row.IsHtml ? value : Html(value));
    }

    private static void AppendLienNotes(StringBuilder html, Lien lien)
    {
        var notes = FirstNonEmpty(lien.Description, lien.Notes);
        if (string.IsNullOrWhiteSpace(notes))
            return;

        html.AppendLine("<div class=\"notes-block\">");
        html.AppendLine("<label>Lien Notes</label>");
        html.Append("<p>")
            .Append(Html(notes))
            .AppendLine("</p>");
        html.AppendLine("</div>");
    }

    private static void AppendDocumentsCard(StringBuilder html, IReadOnlyList<PublicDocumentView> documents)
    {
        html.AppendLine("<section class=\"card documents-card\" aria-labelledby=\"documents-title\">");
        html.Append("<div class=\"card-title\">");
        AppendIcon(html, "chevron-down", "chevron");
        html.Append("<h2 id=\"documents-title\">Documents");
        if (documents.Count > 0)
        {
            html.Append(" (")
                .Append(documents.Count.ToString(CultureInfo.InvariantCulture))
                .Append(')');
        }
        html.AppendLine("</h2></div>");

        if (documents.Count == 0)
        {
            html.Append("<div class=\"empty-state\"><div class=\"avatar\" aria-hidden=\"true\">");
            AppendIcon(html, "file-text");
            html.AppendLine("</div><p>No supporting documents are available for this lien.</p></div>");
        }
        else
        {
            html.AppendLine("<div class=\"document-list\">");
            foreach (var document in documents)
            {
                html.AppendLine("<article class=\"document-row\">");
                html.Append("<div class=\"document-left\"><div class=\"avatar\" aria-hidden=\"true\">");
                AppendIcon(html, "file-text");
                html.AppendLine("</div><div class=\"document-text\">");
                html.Append("<div class=\"document-name\">")
                    .Append(Html(document.FileName))
                    .AppendLine("</div>");
                html.Append("<div class=\"document-meta\"><span>")
                    .Append(Html(document.Category ?? "Document"));
                if (!string.IsNullOrWhiteSpace(document.SizeOrType))
                {
                    html.Append("</span><span>&middot;</span><span>")
                        .Append(Html(document.SizeOrType));
                }
                html.AppendLine("</span></div>");
                html.AppendLine("</div></div>");
                html.Append("<div class=\"document-actions\"><button class=\"icon-button\" type=\"button\" aria-label=\"View document\">");
                AppendIcon(html, "eye");
                html.Append("</button><button class=\"icon-button\" type=\"button\" aria-label=\"Download document\">");
                AppendIcon(html, "download");
                html.AppendLine("</button></div>");
                html.AppendLine("</article>");
            }
            html.AppendLine("</div>");
        }

        html.AppendLine("</section>");
    }

    private static void AppendMessagesCard(StringBuilder html)
    {
        html.AppendLine("<section class=\"card documents-card\" aria-labelledby=\"messages-title\">");
        html.Append("<div class=\"card-title\">");
        AppendIcon(html, "chevron-down", "chevron");
        html.AppendLine("<h2 id=\"messages-title\">Messages</h2></div>");
        html.Append("<div class=\"empty-state\"><div class=\"avatar\" aria-hidden=\"true\">");
        AppendIcon(html, "message-square-more");
        html.AppendLine("</div><p>No messages yet. Send a message to the seller below.</p></div>");
        html.Append("<div class=\"message-input\"><input aria-label=\"Message\" placeholder=\"Type a message...\" maxlength=\"400\"><span class=\"counter\">0/400</span><button class=\"send\" type=\"button\" aria-label=\"Send message\">");
        AppendIcon(html, "send");
        html.AppendLine("</button></div>");
        html.AppendLine("</section>");
    }

    private static string ResolveDisplayHost(HttpContext httpContext)
    {
        var headers = httpContext.Request.Headers;
        var host = FirstNonEmpty(
            headers["X-Legal-Synq-Public-Host"].FirstOrDefault(),
            headers["X-Forwarded-Host"].FirstOrDefault(),
            ResolveForwardedHost(headers["Forwarded"].FirstOrDefault()),
            httpContext.Request.Host.Value,
            "secure link");

        return host!;
    }

    private static string? ResolveForwardedHost(string? forwardedHeader)
    {
        if (string.IsNullOrWhiteSpace(forwardedHeader))
            return null;

        foreach (var segment in forwardedHeader.Split(';', ','))
        {
            var pair = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length == 2 && string.Equals(pair[0], "host", StringComparison.OrdinalIgnoreCase))
                return pair[1].Trim('"');
        }

        return null;
    }

    private static string ResolveSectionIcon(string title)
        => title.Contains("Funding", StringComparison.OrdinalIgnoreCase)
            ? "building-2"
            : "file-text";

    private static void AppendIcon(StringBuilder html, string icon, string? className = null)
    {
        html.Append("<span class=\"icon");
        if (!string.IsNullOrWhiteSpace(className))
        {
            html.Append(' ')
                .Append(className);
        }
        html.Append("\" aria-hidden=\"true\">");
        html.Append(icon switch
        {
            "building-2" => """
<svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18"/><path d="M6 12H4a2 2 0 0 0-2 2v8h20v-8a2 2 0 0 0-2-2h-2"/><path d="M10 6h4"/><path d="M10 10h4"/><path d="M10 14h4"/><path d="M10 18h4"/></svg>
""",
            "chevron-down" => """
<svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg>
""",
            "download" => """
<svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><path d="M7 10l5 5 5-5"/><path d="M12 15V3"/></svg>
""",
            "eye" => """
<svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2.062 12.348a1 1 0 0 1 0-.696A10.75 10.75 0 0 1 12 5a10.75 10.75 0 0 1 9.938 6.652 1 1 0 0 1 0 .696A10.75 10.75 0 0 1 12 19a10.75 10.75 0 0 1-9.938-6.652Z"/><circle cx="12" cy="12" r="3"/></svg>
""",
            "message-square-more" => """
<svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a4 4 0 0 1-4 4H7l-4 4V7a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4z"/><path d="M8 10h.01"/><path d="M12 10h.01"/><path d="M16 10h.01"/></svg>
""",
            "send" => """
<svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m22 2-7 20-4-9-9-4Z"/><path d="M22 2 11 13"/></svg>
""",
            _ => """
<svg viewBox="0 0 24 24" fill="none" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/><path d="M10 9H8"/><path d="M16 13H8"/><path d="M16 17H8"/></svg>
"""
        });
        html.Append("</span>");
    }

    private static string RenderLinkStatePage(HttpContext httpContext, string title, string message)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("<title>")
            .Append(Html(title))
            .AppendLine("</title>");
        AppendStyles(html);
        html.AppendLine("</head><body>");
        html.AppendLine("<main class=\"portal-shell\">");
        AppendSafariToolbar(html, httpContext);
        AppendPortalHeader(html);
        html.AppendLine("<section class=\"state-page\"><div class=\"state-card\">");
        html.Append("<h1>")
            .Append(Html(title))
            .AppendLine("</h1>");
        html.Append("<p>")
            .Append(Html(message))
            .AppendLine("</p>");
        html.AppendLine("</div></section>");
        html.AppendLine("</main></body></html>");
        return html.ToString();
    }

    private static IResult HtmlResult(string html, int statusCode)
        => Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8, statusCode);

    private static Contact? SelectSellerContact(IReadOnlyList<Contact> contacts)
        => contacts.FirstOrDefault(c =>
               string.Equals(c.ContactType, ContactType.LawFirm, StringComparison.Ordinal) &&
               string.IsNullOrWhiteSpace(c.ContactSubtype) &&
               !string.IsNullOrWhiteSpace(c.Email))
           ?? contacts.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Email))
           ?? contacts.FirstOrDefault();

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        foreach (var segment in notes.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value;
        }

        return result;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string ResolveLienCode(Lien lien)
        => string.IsNullOrWhiteSpace(lien.LienNumber) ? lien.Id.ToString() : lien.LienNumber;

    private static string? FormatDate(DateOnly? date)
        => date?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

    private static string FormatDateTime(DateTime value)
        => $"{value:MM/dd/yyyy} &middot; {value:hh:mm:ss tt}";

    private static string FormatDocumentSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (!long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
            return trimmed;

        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024m * 1024m):0.#} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024m:0.#} KB";

        return $"{bytes} B";
    }

    private static string? ResolveFileExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(extension)
            ? null
            : extension.TrimStart('.').ToUpperInvariant();
    }

    private static string HumanizeDocumentTaskType(string taskType)
        => taskType switch
        {
            "LegacyCaseDocument" => "Case Document",
            "LegacyLienDocument" => "Lien Document",
            "LegacyMedicalDocument" => "Medical Document",
            _ => "Document",
        };

    private static string Html(string value)
        => WebUtility.HtmlEncode(value);

    private sealed record PublicPortalView(
        SellingBuyerAccessLink AccessLink,
        Lien Lien,
        Case? Case,
        Contact? BuyerContact,
        Contact? SellerContact,
        string? HandlingLawFirm,
        string? CaseManager,
        IReadOnlyList<PublicDocumentView> Documents);

    private sealed record PublicDocumentView(string FileName, string? Category, string SizeOrType);

    private sealed record DataRow(string Label, string? Value, bool IsHtml = false);
}
