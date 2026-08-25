using Liens.Application.Interfaces;

namespace Liens.Application.Services;

internal static class ConfirmSaleEmailAssets
{
    internal const string LegalSynqBrandIconContentId = "legalsynq-brand-icon";
    internal const string SellerInformationIconContentId = "seller-information-icon";
    internal const string AssetOverviewIconContentId = "asset-overview-icon";
    internal const string SupportingDocumentsIconContentId = "supporting-documents-icon";

    private const string ResourcePrefix = "Liens.Application.EmailAssets.";

    internal static IReadOnlyList<NotificationEmailInlineAttachment> InlineAttachments { get; } =
    [
        BuildInlineAttachment(LegalSynqBrandIconContentId, "legalsynq-brand-icon.png"),
        BuildInlineAttachment(SellerInformationIconContentId, "seller-information-icon.png"),
        BuildInlineAttachment(AssetOverviewIconContentId, "asset-overview-icon.png"),
        BuildInlineAttachment(SupportingDocumentsIconContentId, "supporting-documents-icon.png"),
    ];

    private static NotificationEmailInlineAttachment BuildInlineAttachment(
        string contentId,
        string fileName)
        => new(contentId, fileName, "image/png", LoadBase64Content(fileName));

    private static string LoadBase64Content(string fileName)
    {
        var resourceName = ResourcePrefix + fileName;
        using var stream = typeof(ConfirmSaleEmailAssets).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded email asset '{resourceName}' was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Convert.ToBase64String(memory.ToArray());
    }
}
