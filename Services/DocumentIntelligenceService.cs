using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Configuration;

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

    public async Task<string> ExtractTextAsync(string pdfPath)
    {
        Console.WriteLine($"Extrayendo texto del PDF: {Path.GetFileName(pdfPath)}...");
        await using var stream = File.OpenRead(pdfPath);
        
        var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-layout", BinaryData.FromStream(stream));
        
        var result = operation.Value;
        return result.Content;
    }
}
