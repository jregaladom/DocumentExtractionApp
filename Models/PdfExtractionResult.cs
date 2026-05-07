namespace PdfProcessorApp.Models;

/// <summary>
/// Encapsulates the LLM-optimized text extracted from a PDF document.
/// Both properties contain clean, structured markdown-like text
/// with noise removed and content organized for minimal token usage.
/// </summary>
public class PdfExtractionResult
{
    /// <summary>
    /// Full document content across all pages, structured and cleaned.
    /// </summary>
    public string FullContent { get; set; } = string.Empty;

    /// <summary>
    /// Content from only the first page, structured and cleaned.
    /// </summary>
    public string FirstPageContent { get; set; } = string.Empty;
}
