using PdfProcessorApp.Services;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Diagnostics;

using PdfProcessorApp.Models;

namespace PdfProcessorApp;

public class PdfProcessor
{
    private readonly DocumentIntelligenceService _docIntellService;
    private readonly SemanticKernelService _skService;
    private readonly List<DocumentTypeConfig> _documentTypes;

    public PdfProcessor(DocumentIntelligenceService docIntellService, SemanticKernelService skService, List<DocumentTypeConfig> documentTypes)
    {
        _docIntellService = docIntellService;
        _skService = skService;
        _documentTypes = documentTypes;
    }

    public async Task<(string FileName, string DocumentType, bool IsComplete, string MissingItems)> ProcessFileAsync(string pdfPath)
    {
        string fileName = Path.GetFileName(pdfPath);
        string directory = Path.GetDirectoryName(pdfPath) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(pdfPath);

        string extractedTxtPath = Path.Combine(directory, $"{baseName}_extracted.txt");
        string cleanedTxtPath = Path.Combine(directory, $"{baseName}_cleaned.txt");
        string analysisJsonPath = Path.Combine(directory, $"{baseName}_analysis.json");
        string reportFilePath = Path.Combine(directory, "processing_report.txt");

        string identifiedType = "Unclassified";
        bool isComplete = false;
        string missingItems = string.Empty;

        try
        {
            Console.WriteLine($"\n--- Procesando: {fileName} ---");
            
            var totalSw = Stopwatch.StartNew();
            var stepSw = new Stopwatch();

            // 1. Extract text
            stepSw.Start();
            var (fullText, firstPageText) = await _docIntellService.ExtractTextAsync(pdfPath);
            stepSw.Stop();
            var extractionTime = stepSw.Elapsed;
            
            await File.WriteAllTextAsync(extractedTxtPath, fullText);
            Console.WriteLine($"[✓] Guardado texto crudo (completo) en: {Path.GetFileName(extractedTxtPath)}");

            // 1.5 Pre-clean text with C# (solo primera página)
            stepSw.Restart();
            string preCleanedText = Utils.TextCleaner.PreClean(firstPageText);

            // 2. Clean text
            string cleanedText = await _skService.CleanTextAsync(preCleanedText);
            stepSw.Stop();
            var cleaningTime = stepSw.Elapsed;
            
            await File.WriteAllTextAsync(cleanedTxtPath, cleanedText);
            Console.WriteLine($"[✓] Guardado texto limpio en: {Path.GetFileName(cleanedTxtPath)}");

            // 3. Classify and validate document
            stepSw.Restart();
            var classificationResult = await _skService.ClassifyDocumentAsync(cleanedText, _documentTypes);
            stepSw.Stop();
            var classificationTime = stepSw.Elapsed;
            
            identifiedType = classificationResult.DocumentType;
            isComplete = classificationResult.IsComplete;
            missingItems = classificationResult.MissingItems;

            Console.WriteLine($"[✓] Documento clasificado como: {identifiedType} (Completo: {isComplete})");
            if (!isComplete && !string.IsNullOrEmpty(missingItems))
            {
                Console.WriteLine($"[!] Faltante: {missingItems}");
            }

            TimeSpan extractionConceptsTime = TimeSpan.Zero;

            // 4. Extract concepts if classified
            if (identifiedType != "Unclassified")
            {
                var docTypeConfig = _documentTypes.FirstOrDefault(dt => dt.Name.Equals(identifiedType, StringComparison.OrdinalIgnoreCase));
                if (docTypeConfig != null)
                {
                    stepSw.Restart();
                    var analysisResult = await _skService.ExtractConceptsAsync(cleanedText, fileName, identifiedType, docTypeConfig.ExtractionPrompt);
                    stepSw.Stop();
                    extractionConceptsTime = stepSw.Elapsed;

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
                else
                {
                    Console.WriteLine($"[x] El tipo identificado '{identifiedType}' no coincide con la configuración.");
                    identifiedType = "Unclassified";
                }
            }
            else
            {
                Console.WriteLine($"[-] Documento no clasificado. Se omite la extracción de conceptos.");
            }

            totalSw.Stop();
            var totalTime = totalSw.Elapsed;

            // Generate report
            string reportLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Documento: {fileName} | Extracción: {extractionTime.TotalSeconds:F2}s | Limpieza: {cleaningTime.TotalSeconds:F2}s | Clasificación: {classificationTime.TotalSeconds:F2}s | Extracción LLM: {extractionConceptsTime.TotalSeconds:F2}s | Total: {totalTime.TotalSeconds:F2}s\n";
            await File.AppendAllTextAsync(reportFilePath, reportLine);
            Console.WriteLine($"[✓] Reporte de tiempos actualizado en: {Path.GetFileName(reportFilePath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[x] Error procesando el archivo {fileName}: {ex.Message}");
        }

        return (fileName, identifiedType, isComplete, missingItems);
    }
}
