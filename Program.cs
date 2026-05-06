using Microsoft.Extensions.Configuration;
using PdfProcessorApp;
using PdfProcessorApp.Services;

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

IConfiguration config = builder.Build();

try
{
    var docIntellService = new DocumentIntelligenceService(config);
    var skService = new SemanticKernelService(config);
    var pdfProcessor = new PdfProcessor(docIntellService, skService);

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

    foreach (var pdfFile in pdfFiles)
    {
        await pdfProcessor.ProcessFileAsync(pdfFile);
    }

    Console.WriteLine("\nProcesamiento finalizado.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error de inicialización: {ex.Message}");
    Console.WriteLine("Por favor, asegúrate de haber configurado los endpoints y api keys en appsettings.json.");
}
