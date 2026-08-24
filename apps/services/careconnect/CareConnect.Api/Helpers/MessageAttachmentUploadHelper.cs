using CareConnect.Api.Options;
using CareConnect.Application.DTOs;

namespace CareConnect.Api.Helpers;

internal static class MessageAttachmentUploadHelper
{
    public const int MaxMessageLength = 4000;
    public const int MaxFilesPerMessage = 10;

    public static string NormalizeMessage(string? message) => message?.Trim() ?? string.Empty;

    public static IResult? ValidateMessage(string message, IFormFileCollection files)
    {
        if (message.Length == 0 && files.Count == 0)
        {
            return Results.BadRequest(new
            {
                error = "message or at least one attachment is required."
            });
        }

        if (message.Length > MaxMessageLength)
        {
            return Results.BadRequest(new
            {
                error = $"message must be {MaxMessageLength} characters or fewer."
            });
        }

        return null;
    }

    public static IResult? ValidateFiles(IFormFileCollection files, AttachmentUploadOptions options)
    {
        if (files.Count > MaxFilesPerMessage)
        {
            return Results.BadRequest(new
            {
                error = $"A message can include at most {MaxFilesPerMessage} attachments."
            });
        }

        foreach (var file in files)
        {
            var validationError = ValidateFile(file, options);
            if (validationError is not null)
                return validationError;
        }

        return null;
    }

    public static List<ReferralMessageAttachmentUpload> OpenUploads(IFormFileCollection files)
    {
        var uploads = new List<ReferralMessageAttachmentUpload>(files.Count);
        foreach (var file in files)
        {
            uploads.Add(new ReferralMessageAttachmentUpload(
                file.OpenReadStream(),
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                file.Length));
        }

        return uploads;
    }

    public static void DisposeUploads(IEnumerable<ReferralMessageAttachmentUpload> uploads)
    {
        foreach (var upload in uploads)
            upload.FileContent.Dispose();
    }

    private static IResult? ValidateFile(IFormFile file, AttachmentUploadOptions options)
    {
        if (file.Length > options.MaxFileSizeBytes)
        {
            var limitMb = options.MaxFileSizeBytes / (1024 * 1024);
            return Results.BadRequest(new
            {
                error = $"File size {file.Length:N0} bytes exceeds the maximum allowed size of {limitMb} MB ({options.MaxFileSizeBytes:N0} bytes)."
            });
        }

        var normalizedContentType = file.ContentType?.Split(';')[0].Trim().ToLowerInvariant() ?? string.Empty;
        if (!options.AllowedContentTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                error = $"Content type '{file.ContentType}' is not permitted.",
                allowed = options.AllowedContentTypes
            });
        }

        return null;
    }
}
