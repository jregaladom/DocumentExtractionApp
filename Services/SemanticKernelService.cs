using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using PdfProcessorApp.Models;
using System.Text.Json;

namespace PdfProcessorApp.Services;

public class SemanticKernelService
{
    private readonly Kernel _kernel;

    public SemanticKernelService(IConfiguration config)
    {
        string modelId = config["OpenAI:ModelId"] ?? "gpt-4o";
        string apiKey = config["OpenAI:ApiKey"] ?? throw new ArgumentNullException("OpenAI:ApiKey missing");

        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(modelId, apiKey);
        _kernel = builder.Build();
    }

    public async Task<string> CleanTextAsync(string rawText)
    {
        Console.WriteLine("Limpiando y estructurando el texto con LLM...");
        var prompt = @"
Por favor, toma el siguiente texto extraído de un documento PDF y límpialo.
Tu objetivo es devolver el texto de la manera más ordenada, estructurada y entendible posible para que otro modelo de IA pueda procesarlo fácilmente.
Elimina encabezados o pies de página repetitivos, corrige saltos de línea incorrectos y organiza la información de forma lógica. No resumas ni elimines información valiosa, solo mejora la estructura y limpieza.

Texto original:
{{$input}}
";
        var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments { ["input"] = rawText });
        return result.ToString();
    }

    public async Task<DocumentAnalysisResult?> AnalyzeTextAsync(string cleanedText, string fileName)
    {
        Console.WriteLine($"Analizando conceptos y clasificando documento: {fileName}...");
        var prompt = @"
Analiza el siguiente documento de texto y extrae la información en un formato JSON estricto.
El JSON debe contener:
- fileName: El nombre del archivo (usa el proporcionado: {{$fileName}}).
- documentType: Clasifica qué tipo de documento es de acuerdo a su contenido (ej. Factura, Contrato, Reporte, Currículum, etc.).
- concepts: Una lista de objetos donde cada uno tiene 'name' (nombre del concepto/entidad encontrado) y 'value' (el valor asociado).

Texto:
{{$input}}

Responde EXCLUSIVAMENTE con el objeto JSON, sin formato de markdown (sin ```json) y sin texto adicional.
";
        var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments { 
            ["input"] = cleanedText,
            ["fileName"] = fileName
        });

        var jsonString = result.ToString().Trim();
        if (jsonString.StartsWith("```json"))
        {
            jsonString = jsonString.Substring(7);
            if (jsonString.EndsWith("```"))
            {
                jsonString = jsonString.Substring(0, jsonString.Length - 3);
            }
        }
        else if (jsonString.StartsWith("```"))
        {
            jsonString = jsonString.Substring(3);
            if (jsonString.EndsWith("```"))
            {
                jsonString = jsonString.Substring(0, jsonString.Length - 3);
            }
        }
        jsonString = jsonString.Trim();

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var analysisResult = JsonSerializer.Deserialize<DocumentAnalysisResult>(jsonString, options);
            return analysisResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al deserializar el JSON del LLM: {ex.Message}");
            Console.WriteLine($"JSON crudo: {jsonString}");
            return null;
        }
    }
}
