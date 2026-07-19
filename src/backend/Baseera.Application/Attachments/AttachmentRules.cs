namespace Baseera.Application.Attachments;

using System.Security.Cryptography;
using Baseera.Domain.Attachments;

public static class AttachmentEntityTypes
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "Organization",
        "Region",
        "Facility",
        "FacilityUnit",
        "Building",
        "Department",
        "User"
    };

    public static bool IsAllowed(string entityType) => Allowed.Contains(entityType);
}

public static class AttachmentRules
{
    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "text/plain",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public const long MaxSizeBytes = 10 * 1024 * 1024;

    public static string SanitizeFileName(string original)
    {
        var name = Path.GetFileName(original);
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        name = name.Replace("..", "_");
        return string.IsNullOrWhiteSpace(name) ? "file.bin" : name;
    }

    public static string ComputeSha256(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var hash = SHA256.HashData(stream);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static void ValidateMagicBytes(Stream stream, string contentType, string fileName)
    {
        if (!stream.CanSeek)
        {
            throw new InvalidOperationException("تعذر التحقق من توقيع الملف.");
        }

        stream.Position = 0;
        Span<byte> header = stackalloc byte[8];
        var read = stream.Read(header);
        stream.Position = 0;
        if (read < 4)
        {
            throw new InvalidOperationException("الملف قصير جدًا أو تالف.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var ok = contentType.ToLowerInvariant() switch
        {
            "application/pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
            "image/png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            "image/jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "text/plain" => true,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                => header[0] == 0x50 && header[1] == 0x4B,
            _ => false
        };

        if (!ok)
        {
            throw new InvalidOperationException("توقيع الملف لا يطابق النوع المعلن.");
        }

        if (ext is ".exe" or ".dll" or ".bat" or ".cmd" or ".ps1" or ".sh")
        {
            throw new InvalidOperationException("امتداد الملف غير مسموح.");
        }
    }
}
