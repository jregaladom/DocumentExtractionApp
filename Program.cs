using Microsoft.Extensions.Configuration;
using PdfProcessorApp;
using PdfProcessorApp.Services;
using PdfProcessorApp.Models;
using System.Text;

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

IConfiguration config = builder.Build();

try
{
    var docTypes = config.GetSection("DocumentTypes").Get<List<DocumentTypeConfig>>() ?? new List<DocumentTypeConfig>();

    var docIntellService = new DocumentIntelligenceService(config);
    var skService = new SemanticKernelService(config);
    var pdfProcessor = new PdfProcessor(docIntellService, skService, docTypes);

    string pdfFolder = config["PdfFolder"] ?? "./PdfInput";

    // Ensure the folder exists
    if (!Directory.Exists(pdfFolder))
    {
        Directory.CreateDirectory(pdfFolder);
        Console.WriteLine($"Carpeta '{pdfFolder}' creada. Por favor coloca tus archivos PDF allí y vuelve a ejecutar.");
        return;
    }

    var pdfFiles = Directory.GetFiles(pdfFolder, "*.pdf");

    if (pdfFiles.Length == 0)
    {
        Console.WriteLine($"No se encontraron archivos PDF en la carpeta '{pdfFolder}'.");
        return;
    }

    Console.WriteLine($"Encontrados {pdfFiles.Length} archivo(s) PDF en '{pdfFolder}'. Iniciando procesamiento...");

    var results = new List<(string FileName, string DocumentType, bool IsComplete, string MissingItems)>();

    foreach (var pdfFile in pdfFiles)
    {
        var result = await pdfProcessor.ProcessFileAsync(pdfFile);
        results.Add(result);
    }

    // Generate Global Report
    var sb = new StringBuilder();
    sb.AppendLine("=== REPORTE GLOBAL DE CLASIFICACIÓN Y COMPLETITUD ===");
    sb.AppendLine($"Total de documentos procesados: {results.Count}\n");

    var knownTypes = docTypes.Select(dt => dt.Name).ToList();

    foreach (var knownType in knownTypes)
    {
        var docsOfType = results.Where(r => r.DocumentType.Equals(knownType, StringComparison.OrdinalIgnoreCase)).ToList();
        if (docsOfType.Any())
        {
            sb.AppendLine($"[✓] {knownType} - Encontrado en:");
            foreach (var doc in docsOfType)
            {
                string status = doc.IsComplete ? "(Completo)" : $"(Incompleto - Falta: {doc.MissingItems})";
                sb.AppendLine($"    - {doc.FileName} {status}");
            }
        }
        else
        {
            sb.AppendLine($"[x] {knownType} - No encontrado en ningún documento.");
        }
        sb.AppendLine();
    }

    var unclassified = results.Where(r => r.DocumentType == "Unclassified").ToList();
    if (unclassified.Any())
    {
        sb.AppendLine($"[-] Documentos No Clasificados:");
        foreach (var doc in unclassified)
        {
            sb.AppendLine($"    - {doc.FileName}");
        }
    }

    string reportPath = "classification_summary_report.txt";
    await File.WriteAllTextAsync(reportPath, sb.ToString());

    Console.WriteLine($"\nProcesamiento finalizado. Reporte global guardado en: {reportPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error de inicialización: {ex.Message}");
    Console.WriteLine("Por favor, asegúrate de haber configurado los endpoints y api keys en appsettings.json.");
}
