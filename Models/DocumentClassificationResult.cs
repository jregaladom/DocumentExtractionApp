namespace PdfProcessorApp.Models;

public class DocumentClassificationResult
{
    public string DocumentType { get; set; } = "Unclassified";
    public bool IsComplete { get; set; } = false;
    public string MissingItems { get; set; } = string.Empty;
}
