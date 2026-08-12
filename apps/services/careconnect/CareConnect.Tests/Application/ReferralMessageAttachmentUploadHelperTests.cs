using CareConnect.Api.Helpers;
using CareConnect.Api.Options;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CareConnect.Tests.Application;

public class ReferralMessageAttachmentUploadHelperTests
{
    [Fact]
    public void ValidateMessage_AllowsBlankMessageWhenFilesAreSelected()
    {
        var files = new FormFileCollection
        {
            MakeFile("scan.png", "image/png", 1024)
        };

        var result = MessageAttachmentUploadHelper.ValidateMessage(string.Empty, files);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateMessage_RejectsBlankMessageWithoutFiles()
    {
        var result = MessageAttachmentUploadHelper.ValidateMessage(string.Empty, new FormFileCollection());

        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateMessage_RejectsOverLengthMessage()
    {
        var message = new string('x', MessageAttachmentUploadHelper.MaxMessageLength + 1);

        var result = MessageAttachmentUploadHelper.ValidateMessage(message, new FormFileCollection());

        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateFiles_AllowsConfiguredContentTypesWithinLimits()
    {
        var files = new FormFileCollection
        {
            MakeFile("scan.png", "image/png", 1024)
        };

        var result = MessageAttachmentUploadHelper.ValidateFiles(files, new AttachmentUploadOptions());

        Assert.Null(result);
    }

    [Fact]
    public void ValidateFiles_RejectsUnsupportedContentType()
    {
        var files = new FormFileCollection
        {
            MakeFile("script.exe", "application/x-msdownload", 1024)
        };

        var result = MessageAttachmentUploadHelper.ValidateFiles(files, new AttachmentUploadOptions());

        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateFiles_RejectsOversizedFile()
    {
        var files = new FormFileCollection
        {
            MakeFile("large.pdf", "application/pdf", 2048)
        };
        var options = new AttachmentUploadOptions { MaxFileSizeBytes = 1024 };

        var result = MessageAttachmentUploadHelper.ValidateFiles(files, options);

        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateFiles_RejectsMoreThanTenFiles()
    {
        var files = new FormFileCollection();
        for (var i = 0; i < MessageAttachmentUploadHelper.MaxFilesPerMessage + 1; i++)
        {
            files.Add(MakeFile($"scan-{i}.png", "image/png", 1024));
        }

        var result = MessageAttachmentUploadHelper.ValidateFiles(files, new AttachmentUploadOptions());

        Assert.NotNull(result);
    }

    private static FormFile MakeFile(string fileName, string contentType, long length)
    {
        var stream = new MemoryStream(new byte[(int)length]);
        return new FormFile(stream, 0, length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }
}
