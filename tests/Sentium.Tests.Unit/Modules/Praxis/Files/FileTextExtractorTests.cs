using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Sentium.Praxis.Features.Files;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Files;

public sealed class FileTextExtractorTests
{
    private readonly FileTextExtractor _extractor = new();

    [Fact]
    public async Task PlainText_IsDecodedAsUtf8_ByDefault()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Grüße, мир"));

        var text = await _extractor.ExtractAsync(stream, ".md", TestContext.Current.CancellationToken);

        Assert.Equal("Grüße, мир", text);
    }

    [Fact]
    public async Task PlainText_HonoursAByteOrderMark()
    {
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        using var stream = new MemoryStream([.. encoding.GetPreamble(), .. encoding.GetBytes("utf-16 text")]);

        var text = await _extractor.ExtractAsync(stream, ".txt", TestContext.Current.CancellationToken);

        Assert.Equal("utf-16 text", text);
    }

    [Fact]
    public async Task Pdf_YieldsEachPagesText_WithPageMarkers()
    {
        using var stream = new MemoryStream(BuildPdf("Quarterly revenue grew", "Second page notes"));

        var text = await _extractor.ExtractAsync(stream, ".PDF", TestContext.Current.CancellationToken);

        Assert.Contains("--- Page 1 ---", text, StringComparison.Ordinal);
        Assert.Contains("Quarterly revenue grew", text, StringComparison.Ordinal);
        Assert.Contains("--- Page 2 ---", text, StringComparison.Ordinal);
        Assert.Contains("Second page notes", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Docx_YieldsParagraphText_IncludingTables()
    {
        using var stream = new MemoryStream(BuildDocx());

        var text = await _extractor.ExtractAsync(stream, ".docx", TestContext.Current.CancellationToken);

        Assert.Contains("Meeting notes", text, StringComparison.Ordinal);
        Assert.Contains("Cell value", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CorruptPdf_ThrowsInvalidData()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("not a pdf at all"));

        var extract = () => _extractor.ExtractAsync(stream, ".pdf", TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<InvalidDataException>(extract);
    }

    private static byte[] BuildPdf(params string[] pages)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var line in pages)
        {
            builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4).AddText(line, 12, new PdfPoint(25, 700), font);
        }

        return builder.Build();
    }

    private static byte[] BuildDocx()
    {
        using var buffer = new MemoryStream();
        using (var document = WordprocessingDocument.Create(buffer, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(
                new Paragraph(new Run(new Text("Meeting notes"))),
                new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("Cell value"))))))));
        }

        return buffer.ToArray();
    }
}
