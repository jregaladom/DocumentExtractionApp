using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Configuration;
using PdfProcessorApp.Models;
using System.Text;

namespace PdfProcessorApp.Services;

public class DocumentIntelligenceService
{
    private readonly DocumentIntelligenceClient _client;

    public DocumentIntelligenceService(IConfiguration config)
    {
        string endpoint = config["AzureDocumentIntelligence:Endpoint"] ?? throw new ArgumentNullException("AzureDocumentIntelligence:Endpoint missing");
        string apiKey = config["AzureDocumentIntelligence:ApiKey"] ?? throw new ArgumentNullException("AzureDocumentIntelligence:ApiKey missing");

        var credential = new AzureKeyCredential(apiKey);
        _client = new DocumentIntelligenceClient(new Uri(endpoint), credential);
    }

    /// <summary>
    /// Extracts text from a PDF using Document Intelligence's prebuilt-layout model.
    /// Uses structural information (paragraphs with semantic roles, tables) to produce
    /// clean, well-organized, LLM-optimized markdown-like output.
    /// Returns both the full document content and the first page content.
    /// </summary>
    public async Task<PdfExtractionResult> ExtractTextAsync(string pdfPath)
    {
        Console.WriteLine($"Extrayendo texto del PDF: {Path.GetFileName(pdfPath)}...");
        await using var stream = File.OpenRead(pdfPath);

        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-layout",
            BinaryData.FromStream(stream));

        var result = operation.Value;

        // Collect all renderable elements (paragraphs + tables) with their position info
        var allElements = CollectDocumentElements(result);

        // Sort elements by their position in the document (page, then vertical offset)
        allElements.Sort((a, b) =>
        {
            int pageCompare = a.PageNumber.CompareTo(b.PageNumber);
            if (pageCompare != 0) return pageCompare;
            return a.Offset.CompareTo(b.Offset);
        });

        // Build full content and first page content
        var fullContentBuilder = new StringBuilder();
        var firstPageBuilder = new StringBuilder();

        foreach (var element in allElements)
        {
            fullContentBuilder.AppendLine(element.FormattedContent);

            if (element.PageNumber == 1)
            {
                firstPageBuilder.AppendLine(element.FormattedContent);
            }
        }

        // Apply final cleanup
        string fullContent = Utils.TextCleaner.PostClean(fullContentBuilder.ToString());
        string firstPageContent = Utils.TextCleaner.PostClean(firstPageBuilder.ToString());

        return new PdfExtractionResult
        {
            FullContent = fullContent,
            FirstPageContent = firstPageContent
        };
    }

    /// <summary>
    /// Collects all paragraphs and tables from the AnalyzeResult,
    /// filtering out noise (headers, footers, page numbers) and
    /// formatting each element according to its semantic role.
    /// </summary>
    private List<DocumentElement> CollectDocumentElements(AnalyzeResult result)
    {
        var elements = new List<DocumentElement>();

        // Track which content offsets belong to tables so we don't duplicate them
        var tableSpanRanges = new HashSet<(int Offset, int Length)>();

        // Process tables first to know which spans to skip in paragraphs
        if (result.Tables != null)
        {
            foreach (var table in result.Tables)
            {
                int pageNumber = 1;
                int offset = int.MaxValue;

                if (table.BoundingRegions != null && table.BoundingRegions.Count > 0)
                {
                    pageNumber = table.BoundingRegions[0].PageNumber;
                }

                if (table.Spans != null && table.Spans.Count > 0)
                {
                    offset = table.Spans[0].Offset;
                    foreach (var span in table.Spans)
                    {
                        tableSpanRanges.Add((span.Offset, span.Length));
                    }
                }

                string formattedTable = RenderTableAsMarkdown(table);
                if (!string.IsNullOrWhiteSpace(formattedTable))
                {
                    elements.Add(new DocumentElement
                    {
                        PageNumber = pageNumber,
                        Offset = offset,
                        FormattedContent = formattedTable
                    });
                }
            }
        }

        // Process paragraphs
        if (result.Paragraphs != null)
        {
            foreach (var paragraph in result.Paragraphs)
            {
                // Skip noise roles
                string? role = paragraph.Role?.ToString();
                if (IsNoiseRole(role))
                    continue;

                // Skip paragraphs whose spans overlap with table spans
                if (paragraph.Spans != null && paragraph.Spans.Count > 0)
                {
                    bool isInsideTable = paragraph.Spans.Any(ps =>
                        tableSpanRanges.Any(ts =>
                            ps.Offset >= ts.Offset && ps.Offset < ts.Offset + ts.Length));

                    if (isInsideTable)
                        continue;
                }

                string content = paragraph.Content?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                string formatted = FormatParagraphByRole(content, role);

                int pageNumber = 1;
                int offset = 0;

                if (paragraph.BoundingRegions != null && paragraph.BoundingRegions.Count > 0)
                {
                    pageNumber = paragraph.BoundingRegions[0].PageNumber;
                }

                if (paragraph.Spans != null && paragraph.Spans.Count > 0)
                {
                    offset = paragraph.Spans[0].Offset;
                }

                elements.Add(new DocumentElement
                {
                    PageNumber = pageNumber,
                    Offset = offset,
                    FormattedContent = formatted
                });
            }
        }

        return elements;
    }

    /// <summary>
    /// Determines if a paragraph role represents noise that should be excluded.
    /// </summary>
    private static bool IsNoiseRole(string? role)
    {
        if (string.IsNullOrEmpty(role))
            return false;

        return role.Equals("pageHeader", StringComparison.OrdinalIgnoreCase)
            || role.Equals("pageFooter", StringComparison.OrdinalIgnoreCase)
            || role.Equals("pageNumber", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Formats a paragraph's content based on its semantic role.
    /// Uses markdown-like markers for efficient LLM consumption.
    /// </summary>
    private static string FormatParagraphByRole(string content, string? role)
    {
        if (string.IsNullOrEmpty(role))
            return content;

        return role.ToLowerInvariant() switch
        {
            "title" => $"# {content}",
            "sectionheading" => $"## {content}",
            "footnote" => $"[Nota: {content}]",
            "formulablock" => $"[Fórmula: {content}]",
            _ => content
        };
    }

    /// <summary>
    /// Renders a DocumentTable as a clean markdown table.
    /// Uses column headers when available for better LLM understanding.
    /// </summary>
    private static string RenderTableAsMarkdown(DocumentTable table)
    {
        if (table.Cells == null || table.Cells.Count == 0)
            return string.Empty;

        int rowCount = table.RowCount;
        int colCount = table.ColumnCount;

        // Build a grid
        var grid = new string[rowCount, colCount];
        var isHeader = new bool[rowCount, colCount];

        foreach (var cell in table.Cells)
        {
            int r = cell.RowIndex;
            int c = cell.ColumnIndex;

            if (r < rowCount && c < colCount)
            {
                grid[r, c] = cell.Content?.Trim() ?? string.Empty;

                string? kind = cell.Kind?.ToString();
                isHeader[r, c] = kind != null &&
                    (kind.Equals("columnHeader", StringComparison.OrdinalIgnoreCase)
                    || kind.Equals("rowHeader", StringComparison.OrdinalIgnoreCase));
            }
        }

        var sb = new StringBuilder();

        // Determine header row count (rows where all cells are headers)
        int headerRows = 0;
        for (int r = 0; r < rowCount; r++)
        {
            bool allHeaders = true;
            for (int c = 0; c < colCount; c++)
            {
                if (!isHeader[r, c] && !string.IsNullOrEmpty(grid[r, c]))
                {
                    allHeaders = false;
                    break;
                }
            }
            if (allHeaders && r == headerRows)
                headerRows++;
            else
                break;
        }

        // If no explicit header row detected, treat first row as header
        if (headerRows == 0)
            headerRows = 1;

        // Render header rows
        for (int r = 0; r < headerRows; r++)
        {
            sb.Append("| ");
            for (int c = 0; c < colCount; c++)
            {
                sb.Append(grid[r, c] ?? string.Empty);
                sb.Append(" | ");
            }
            sb.AppendLine();
        }

        // Separator
        sb.Append("|");
        for (int c = 0; c < colCount; c++)
        {
            sb.Append("---|");
        }
        sb.AppendLine();

        // Data rows
        for (int r = headerRows; r < rowCount; r++)
        {
            sb.Append("| ");
            for (int c = 0; c < colCount; c++)
            {
                sb.Append(grid[r, c] ?? string.Empty);
                sb.Append(" | ");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Internal helper to represent a positioned document element.
    /// </summary>
    private class DocumentElement
    {
        public int PageNumber { get; set; }
        public int Offset { get; set; }
        public string FormattedContent { get; set; } = string.Empty;
    }
}
