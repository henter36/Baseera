using Baseera.Application.Attachments;

namespace Baseera.UnitTests;

public sealed class AttachmentRulesTests
{
    [Fact]
    public void SanitizeFileName_blocks_path_traversal()
    {
        var name = AttachmentRules.SanitizeFileName("../etc/passwd");
        Assert.DoesNotContain("..", name);
        Assert.Equal("passwd", name);
    }

    [Fact]
    public void AllowedContentTypes_includes_pdf_excludes_exe()
    {
        Assert.Contains("application/pdf", AttachmentRules.AllowedContentTypes);
        Assert.DoesNotContain("application/x-msdownload", AttachmentRules.AllowedContentTypes);
    }

    [Fact]
    public void Entity_type_allowlist_rejects_open_text()
    {
        Assert.True(AttachmentEntityTypes.IsAllowed("Facility"));
        Assert.False(AttachmentEntityTypes.IsAllowed("Anything"));
    }

    [Fact]
    public void Magic_bytes_reject_mismatched_pdf_header()
    {
        using var stream = new MemoryStream("not-a-pdf"u8.ToArray());
        Assert.Throws<InvalidOperationException>(() =>
            AttachmentRules.ValidateMagicBytes(stream, "application/pdf", "a.pdf"));
    }
}
