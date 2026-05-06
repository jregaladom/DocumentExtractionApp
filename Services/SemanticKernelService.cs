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

    public async Task<string> ClassifyDocumentAsync(string cleanedText, List<DocumentTypeConfig> documentTypes)
    {
        Console.WriteLine("Clasificando documento...");
        var rulesConfig = JsonSerializer.Serialize(documentTypes.Select(dt => new { dt.Name, dt.ClassificationRules }));
        
        var prompt = @"
Analiza el siguiente documento y determina a cuál de las siguientes categorías pertenece, basándote en las reglas de clasificación proporcionadas.

Categorías y reglas:
{{$rules}}

Texto del documento:
{{$input}}

Responde EXCLUSIVAMENTE con el nombre exacto de la categoría (por ejemplo, 'Tipo A'). Si el documento no cumple con ninguna de las reglas, responde exactamente con 'Unclassified'. NO agregues ningún otro texto, puntuación ni explicación.
";
        var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments { 
            ["input"] = cleanedText,
            ["rules"] = rulesConfig
        });
        
        return result.ToString().Trim();
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
