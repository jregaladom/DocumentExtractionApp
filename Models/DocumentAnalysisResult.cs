using System.Text.Json.Serialization;

namespace PdfProcessorApp.Models;

public class DocumentAnalysisResult
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("concepts")]
    public List<Concept> Concepts { get; set; } = new();
}

public class Concept
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
