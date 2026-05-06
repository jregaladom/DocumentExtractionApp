namespace PdfProcessorApp.Models;

public class DocumentTypeConfig
{
    public string Name { get; set; } = string.Empty;
    public string ClassificationRules { get; set; } = string.Empty;
    public string ValidationRules { get; set; } = string.Empty;
    public string ExtractionPrompt { get; set; } = string.Empty;
}
