using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Sentium.Praxis.Features.Files;

internal sealed class FileTextExtractor : IFileTextExtractor
{
    public async Task<string> ExtractAsync(Stream content, string extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.Equals(extension, AllowedFileTypes.Pdf, StringComparison.OrdinalIgnoreCase))
        {
            return ExtractPdf(await BufferAsync(content, ct));
        }

        if (string.Equals(extension, AllowedFileTypes.Docx, StringComparison.OrdinalIgnoreCase))
        {
            return ExtractDocx(await BufferAsync(content, ct));
        }

        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    /// <summary>
    /// Both parsers need a seekable stream, which object-store reads are not guaranteed to be.
    /// </summary>
    private static async Task<MemoryStream> BufferAsync(Stream content, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }

    private static string ExtractPdf(MemoryStream buffer)
    {
        try
        {
            using (buffer)
            using (var document = PdfDocument.Open(buffer))
            {
                var text = new StringBuilder();
                foreach (var page in document.GetPages())
                {
                    text.Append("--- Page ").Append(page.Number).AppendLine(" ---");
                    text.AppendLine(ContentOrderTextExtractor.GetText(page).Trim());
                    text.AppendLine();
                }

                return text.ToString().TrimEnd();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidDataException("The PDF could not be read.", ex);
        }
    }

    private static string ExtractDocx(MemoryStream buffer)
    {
        try
        {
            using (buffer)
            using (var document = WordprocessingDocument.Open(buffer, isEditable: false))
            {
                var body = document.MainDocumentPart?.Document?.Body;
                if (body is null)
                {
                    return string.Empty;
                }

                var paragraphs = body.Descendants<Paragraph>().Select(p => p.InnerText);
                return string.Join(Environment.NewLine, paragraphs).Trim();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidDataException("The Word document could not be read.", ex);
        }
    }
}
