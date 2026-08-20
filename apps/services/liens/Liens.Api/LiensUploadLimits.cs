namespace Liens.Api;

public static class LiensUploadLimits
{
    public const long MaxBytes = 50L * 1024 * 1024;
    public const int MaxMegabytes = 50;
    public const long MultipartRequestBytes = 60L * 1024 * 1024;
}
