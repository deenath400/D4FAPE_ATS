namespace Ats.Service.Screening;

using System.Collections.Generic;
using System.Text.Json.Serialization;

// Request models for Google Gemini 2.0 Flash REST API
internal sealed class GeminiRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; set; }

    [JsonPropertyName("systemInstruction")]
    public GeminiSystemInstruction? SystemInstruction { get; set; }
}

internal sealed class GeminiContent
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();
}

internal sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal sealed class GeminiSystemInstruction
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();
}

internal sealed class GeminiGenerationConfig
{
    [JsonPropertyName("responseMimeType")]
    public string ResponseMimeType { get; set; } = "application/json";

    [JsonPropertyName("responseSchema")]
    public GeminiResponseSchema? ResponseSchema { get; set; }
}

internal sealed class GeminiResponseSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "OBJECT";

    [JsonPropertyName("properties")]
    public Dictionary<string, GeminiSchemaProperty> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();
}

internal sealed class GeminiSchemaProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "STRING";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Enum { get; set; }

    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiSchemaProperty? Items { get; set; }
}

// Response models from Google Gemini REST API
internal sealed class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }
}

internal sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiCandidateContent? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

internal sealed class GeminiCandidateContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart>? Parts { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

// Parsed structured JSON output returned by Gemini model
public sealed class GeminiScreeningOutput
{
    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("skillsScore")]
    public int SkillsScore { get; set; }

    [JsonPropertyName("experienceScore")]
    public int ExperienceScore { get; set; }

    [JsonPropertyName("educationScore")]
    public int EducationScore { get; set; }

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = new();

    [JsonPropertyName("concerns")]
    public List<string> Concerns { get; set; } = new();
}
