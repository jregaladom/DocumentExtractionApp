using System.Text;
using System.Text.RegularExpressions;

namespace PdfProcessorApp.Utils;

public static class TextCleaner
{
    /// <summary>
    /// Pre-cleans raw text by normalizing whitespace, removing control characters,
    /// and collapsing redundant blank lines. Used as a first-pass cleanup.
    /// </summary>
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

    /// <summary>
    /// Post-cleans structured/formatted text output.
    /// Designed to be applied after the Document Intelligence structural extraction.
    /// Preserves markdown formatting while removing noise and optimizing for token efficiency.
    /// </summary>
    public static string PostClean(string structuredText)
    {
        if (string.IsNullOrWhiteSpace(structuredText)) return string.Empty;

        // 1. Normalize line breaks
        string text = structuredText.Replace("\r\n", "\n").Replace("\r", "\n");

        // 2. Remove control characters (preserve \n and \t)
        text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // 3. Process line by line
        var lines = text.Split('\n');
        var cleanedLines = new List<string>();
        bool lastWasEmpty = false;

        foreach (var rawLine in lines)
        {
            string line = rawLine.TrimEnd();

            // Preserve markdown heading indentation (# and ##) but trim other lines
            if (!line.StartsWith("#") && !line.StartsWith("|"))
            {
                line = line.TrimStart();
            }

            bool isEmpty = string.IsNullOrWhiteSpace(line);

            if (isEmpty)
            {
                // Allow max one blank line between sections
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

        // 4. Remove leading/trailing blank lines
        string result = string.Join('\n', cleanedLines).Trim();

        // 5. Collapse any remaining excessive whitespace within lines (but not in table pipes)
        result = Regex.Replace(result, @"(?<![\|#])  +(?![\|])", " ");

        return result;
    }
}
