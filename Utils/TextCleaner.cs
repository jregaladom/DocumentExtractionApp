using System.Text.RegularExpressions;

namespace PdfProcessorApp.Utils;

public static class TextCleaner
{
    public static string PreClean(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        // 1. Normalizar saltos de línea a \n
        string text = rawText.Replace("\r\n", "\n").Replace("\r", "\n");

        // 2. Eliminar caracteres de control nulos o extraños (dejamos \n y \t)
        text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // 3. Reemplazar múltiples espacios y tabulaciones por un solo espacio (sin afectar saltos de línea)
        text = Regex.Replace(text, @"[ \t]+", " ");

        // 4. Limpiar espacios al inicio y final de cada línea
        var lines = text.Split('\n')
                        .Select(line => line.Trim())
                        .ToList();

        // 5. Eliminar múltiples líneas en blanco consecutivas (dejando máximo una)
        var cleanedLines = new List<string>();
        bool lastWasEmpty = false;

        foreach (var line in lines)
        {
            bool isEmpty = string.IsNullOrWhiteSpace(line);
            if (isEmpty)
            {
                if (!lastWasEmpty)
                {
                    cleanedLines.Add(string.Empty);
                }
                lastWasEmpty = true;
            }
            else
            {
                cleanedLines.Add(line);
                lastWasEmpty = false;
            }
        }

        // Unir de nuevo el texto
        string finalResult = string.Join('\n', cleanedLines).Trim();

        return finalResult;
    }
}
