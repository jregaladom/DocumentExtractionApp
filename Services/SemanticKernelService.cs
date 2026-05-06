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

    public async Task<DocumentClassificationResult> ClassifyDocumentAsync(string cleanedText, List<DocumentTypeConfig> documentTypes)
    {
        Console.WriteLine("Clasificando y validando documento...");
        var rulesConfig = JsonSerializer.Serialize(documentTypes.Select(dt => new { dt.Name, dt.ClassificationRules, dt.ValidationRules }));
        
        var prompt = @"
Analiza el siguiente documento y realiza dos tareas:
1. Clasificación: Determina a cuál de las siguientes categorías pertenece.
2. Validación: Verifica si el documento está completo basándote en sus reglas de validación.

Categorías y reglas:
{{$rules}}

Texto del documento:
{{$input}}

Debes responder EXCLUSIVAMENTE con un objeto JSON con la siguiente estructura:
{
  ""documentType"": ""Nombre de la categoría o 'Unclassified'"",
  ""isComplete"": true/false,
  ""missingItems"": ""Descripción de lo que falta si isComplete es false, de lo contrario vacío""
}

No agregues ningún otro texto, formato markdown (sin ```json) ni explicación.
";
        var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments { 
            ["input"] = cleanedText,
            ["rules"] = rulesConfig
        });
        
        var jsonString = CleanJsonResponse(result.ToString());
        
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<DocumentClassificationResult>(jsonString, options) ?? new DocumentClassificationResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al deserializar clasificación: {ex.Message}");
            return new DocumentClassificationResult();
        }
    }

    private string CleanJsonResponse(string jsonString)
    {
        jsonString = jsonString.Trim();
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
        return jsonString.Trim();
    }

    public async Task<DocumentAnalysisResult?> ExtractConceptsAsync(string cleanedText, string fileName, string documentType, string extractionPrompt)
    {
        Console.WriteLine($"Extrayendo conceptos para {fileName} como {documentType}...");
        var prompt = @"
Analiza el siguiente documento y extrae la información solicitada en un formato JSON estricto.

Instrucciones de extracción:
{{$extractionPrompt}}

El JSON de respuesta debe tener exactamente esta estructura:
{
  ""fileName"": """ + fileName + @""",
  ""documentType"": """ + documentType + @""",
  ""concepts"": [
    { ""name"": ""nombre_del_concepto"", ""value"": ""valor_extraido"" }
  ]
}

Texto del documento:
{{$input}}

Responde EXCLUSIVAMENTE con el objeto JSON, sin formato de markdown (sin ```json) y sin texto adicional.
";
        var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments { 
            ["input"] = cleanedText,
            ["extractionPrompt"] = extractionPrompt
        });

        var jsonString = CleanJsonResponse(result.ToString());

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
