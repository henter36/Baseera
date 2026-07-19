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
}
