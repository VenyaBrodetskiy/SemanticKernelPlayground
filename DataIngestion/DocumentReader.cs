using SemanticKernelPlayground.Models;

namespace SemanticKernelPlayground.DataInjection;

public class DocumentReader
{
    /// <summary>
    /// Reads the file and returns one TextChunk per non-empty line.
    /// </summary>
    public static IEnumerable<TextChunk> ParseFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var docName = Path.GetFileName(filePath);

        var chunks = new List<TextChunk>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var paragraphId = i + 1;
            var key = $"{docName}_{paragraphId}";

            chunks.Add(new()
            {
                Key = key,
                DocumentName = docName,
                ParagraphId = paragraphId,
                Text = line,
                TextEmbedding = ReadOnlyMemory<float>.Empty
            });
        }

        return chunks;
    }
}
