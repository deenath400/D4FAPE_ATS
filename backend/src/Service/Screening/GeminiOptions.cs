namespace Ats.Service.Screening;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxCvTextLength { get; set; } = 50_000;
}
