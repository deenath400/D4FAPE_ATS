namespace Ats.Service.Screening;

using System.IO;

public interface IPdfTextExtractor
{
    string ExtractText(Stream pdfStream);
}
