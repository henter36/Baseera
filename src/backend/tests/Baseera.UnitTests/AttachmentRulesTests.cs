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
        Assert.True(AttachmentRules.IsAllowedContentType("application/pdf"));
        Assert.False(AttachmentRules.IsAllowedContentType("application/x-msdownload"));
    }

    [Fact]
    public void Entity_type_allowlist_rejects_open_text()
    {
        Assert.True(AttachmentEntityTypes.IsAllowed("Facility"));
        Assert.True(AttachmentEntityTypes.IsAllowed("OperationalNote"));
        Assert.False(AttachmentEntityTypes.IsAllowed("Anything"));
    }

    [Fact]
    public void Magic_bytes_reject_mismatched_pdf_header()
    {
        using var stream = new MemoryStream("not-a-pdf"u8.ToArray());
        Assert.Throws<InvalidOperationException>(() =>
            AttachmentRules.ValidateMagicBytes(stream, "application/pdf", "a.pdf"));
    }

    [Fact]
    public void Text_plain_one_byte_is_accepted()
    {
        using var stream = new MemoryStream([(byte)'a']);
        AttachmentRules.ValidateMagicBytes(stream, "text/plain", "note.txt");
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Text_plain_two_bytes_is_accepted()
    {
        using var stream = new MemoryStream("ab"u8.ToArray());
        AttachmentRules.ValidateMagicBytes(stream, "text/plain", "note.txt");
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Pdf_shorter_than_four_bytes_is_rejected()
    {
        using var stream = new MemoryStream("%PD"u8.ToArray());
        Assert.Throws<InvalidOperationException>(() =>
            AttachmentRules.ValidateMagicBytes(stream, "application/pdf", "a.pdf"));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Jpeg_shorter_than_three_bytes_is_rejected()
    {
        using var stream = new MemoryStream([0xFF, 0xD8]);
        Assert.Throws<InvalidOperationException>(() =>
            AttachmentRules.ValidateMagicBytes(stream, "image/jpeg", "a.jpg"));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Docx_without_zip_magic_is_rejected()
    {
        using var stream = new MemoryStream("NOzip"u8.ToArray());
        Assert.Throws<InvalidOperationException>(() =>
            AttachmentRules.ValidateMagicBytes(
                stream,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "a.docx"));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Xlsx_without_zip_magic_is_rejected()
    {
        using var stream = new MemoryStream([0x00, 0x01]);
        Assert.Throws<InvalidOperationException>(() =>
            AttachmentRules.ValidateMagicBytes(
                stream,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "a.xlsx"));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Valid_pdf_restores_stream_position()
    {
        using var stream = new MemoryStream("%PDF-1.4"u8.ToArray());
        AttachmentRules.ValidateMagicBytes(stream, "application/pdf", "a.pdf");
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task ComputeSha256Async_hashes_and_rewinds()
    {
        using var stream = new MemoryStream("abc"u8.ToArray());
        var hash = await AttachmentRules.ComputeSha256Async(stream);
        Assert.Equal(64, hash.Length);
        Assert.Equal(0, stream.Position);
    }
}
