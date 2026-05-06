using PdfProcessorApp.Services;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace PdfProcessorApp;

public class PdfProcessor
{
    private readonly DocumentIntelligenceService _docIntellService;
    private readonly SemanticKernelService _skService;

    public PdfProcessor(DocumentIntelligenceService docIntellService, SemanticKernelService skService)
    {
        _docIntellService = docIntellService;
        _skService = skService;
    }

    public async Task ProcessFileAsync(string pdfPath)
    {
        string fileName = Path.GetFileName(pdfPath);
        string directory = Path.GetDirectoryName(pdfPath) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(pdfPath);

        string extractedTxtPath = Path.Combine(directory, $"{baseName}_extracted.txt");
        string cleanedTxtPath = Path.Combine(directory, $"{baseName}_cleaned.txt");
        string analysisJsonPath = Path.Combine(directory, $"{baseName}_analysis.json");

        try
        {
            Console.WriteLine($"\n--- Procesando: {fileName} ---");
            
            // 1. Extract text
            string rawText = await _docIntellService.ExtractTextAsync(pdfPath);
            await File.WriteAllTextAsync(extractedTxtPath, rawText);
            Console.WriteLine($"[✓] Guardado texto crudo en: {Path.GetFileName(extractedTxtPath)}");

            // 1.5 Pre-clean text with C#
            string preCleanedText = Utils.TextCleaner.PreClean(rawText);

            // 2. Clean text
            string cleanedText = await _skService.CleanTextAsync(preCleanedText);
            await File.WriteAllTextAsync(cleanedTxtPath, cleanedText);
            Console.WriteLine($"[✓] Guardado texto limpio en: {Path.GetFileName(cleanedTxtPath)}");

            // 3. Analyze text
            var analysisResult = await _skService.AnalyzeTextAsync(cleanedText, fileName);
            if (analysisResult != null)
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                };
                string jsonString = JsonSerializer.Serialize(analysisResult, options);
                await File.WriteAllTextAsync(analysisJsonPath, jsonString);
                Console.WriteLine($"[✓] Guardado análisis JSON en: {Path.GetFileName(analysisJsonPath)}");
            }
            else
            {
                Console.WriteLine($"[x] No se pudo generar el análisis JSON para {fileName}.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[x] Error procesando el archivo {fileName}: {ex.Message}");
        }
    }
}
