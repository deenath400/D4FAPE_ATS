namespace Ats.Service.Screening;

using System;
using System.IO;
using System.Text;
using UglyToad.PdfPig;

public class PdfTextExtractor : IPdfTextExtractor
{
    public string ExtractText(Stream pdfStream)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);

        try
        {
            if (pdfStream.CanSeek)
            {
                pdfStream.Position = 0;
            }

            using var document = PdfDocument.Open(pdfStream);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    sb.AppendLine(page.Text);
                }
            }

            return sb.ToString().Trim();
        }
        catch (Exception)
        {
            // Non-readable, corrupt, or encrypted PDF
            return string.Empty;
        }
    }
}
