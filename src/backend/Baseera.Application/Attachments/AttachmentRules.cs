namespace Baseera.Application.Attachments;

using System.Collections.Frozen;
using System.Security.Cryptography;
using Baseera.Domain.Attachments;

public static class AttachmentEntityTypes
{
    private static readonly FrozenSet<string> Allowed = new[]
    {
        "Organization",
        "Region",
        "Facility",
        "FacilityUnit",
        "Building",
        "Department",
        "User"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsAllowed(string entityType) => Allowed.Contains(entityType);
}

public static class AttachmentRules
{
    private static readonly FrozenSet<string> AllowedContentTypes = new[]
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "text/plain",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public const long MaxSizeBytes = 10 * 1024 * 1024;

    public static bool IsAllowedContentType(string contentType) =>
        AllowedContentTypes.Contains(contentType);

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

    public static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken = default)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Minimum header bytes required to validate a content-type signature.
    /// text/plain has no binary signature requirement (size &gt; 0 is enforced separately).
    /// ZIP-based office formats only check the initial ZIP magic; malware scanning remains deferred.
    /// </summary>
    public static int GetRequiredSignatureLength(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "application/pdf" => 4,
            "image/png" => 4,
            "image/jpeg" => 3,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => 2,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => 2,
            "text/plain" => 0,
            _ => -1
        };

    public static void ValidateMagicBytes(Stream stream, string contentType, string fileName)
    {
        if (!stream.CanSeek)
        {
            throw new InvalidOperationException("تعذر التحقق من توقيع الملف.");
        }

        var required = GetRequiredSignatureLength(contentType);
        if (required < 0)
        {
            throw new InvalidOperationException("نوع الملف غير مسموح.");
        }

        try
        {
            stream.Position = 0;
            Span<byte> header = stackalloc byte[8];
            header.Clear();
            ReadSignatureHeader(stream, header, required);

            if (!MatchesDeclaredSignature(contentType, header))
            {
                throw new InvalidOperationException("توقيع الملف لا يطابق النوع المعلن.");
            }

            RejectDangerousExtension(fileName);
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
        }
    }

    private static void ReadSignatureHeader(Stream stream, Span<byte> header, int required)
    {
        if (required <= 0)
        {
            return;
        }

        try
        {
            stream.ReadExactly(header[..required]);
        }
        catch (EndOfStreamException)
        {
            throw new InvalidOperationException("الملف قصير جدًا أو تالف.");
        }
    }

    private static bool MatchesDeclaredSignature(string contentType, ReadOnlySpan<byte> header) =>
        contentType.ToLowerInvariant() switch
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

    private static void RejectDangerousExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is ".exe" or ".dll" or ".bat" or ".cmd" or ".ps1" or ".sh")
        {
            throw new InvalidOperationException("امتداد الملف غير مسموح.");
        }
    }
}
