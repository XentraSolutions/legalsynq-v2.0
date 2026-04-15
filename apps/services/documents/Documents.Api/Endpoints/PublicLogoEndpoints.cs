using Amazon.S3;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Documents.Api.Endpoints;

public static class PublicLogoEndpoints
{
    private static readonly Guid TenantLogoDocTypeId =
        Guid.Parse("20000000-0000-0000-0000-000000000002");

    public static void MapPublicLogoEndpoints(this WebApplication app)
    {
        app.MapGet("/public/logo/{id:guid}", async (
            Guid id,
            DocsDbContext db,
            IStorageProvider storage,
            CancellationToken ct) =>
        {
            var doc = await db.Documents
                .AsNoTracking()
                .Where(d => d.Id == id
                         && !d.IsDeleted
                         && d.DocumentTypeId == TenantLogoDocTypeId)
                .Select(d => new { d.StorageKey, d.StorageBucket, d.MimeType })
                .FirstOrDefaultAsync(ct);

            if (doc is null || string.IsNullOrEmpty(doc.StorageKey))
                return Results.NotFound();

            try
            {
                var stream = await storage.DownloadAsync(doc.StorageKey, ct);
                return Results.Stream(stream, doc.MimeType ?? "image/png");
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Results.NotFound();
            }
        })
        .AllowAnonymous()
        .WithTags("Public")
        .WithSummary("Public logo access — streams tenant logo images only (document type restricted)")
        .ExcludeFromDescription();
    }
}
