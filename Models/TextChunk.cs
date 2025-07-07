using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;

namespace SemanticKernelPlayground.Models;

public record TextChunk
{
    /// <summary>A unique key for the text paragraph.</summary>
    [VectorStoreKey]
    public required string Key { get; init; }

    /// <summary>A name that points at the original location of the document containing the text.</summary>
    [VectorStoreData]
    public required string DocumentName { get; init; }

    /// <summary>The id of the paragraph from the document containing the text.</summary>
    [VectorStoreData]
    public required int ParagraphId { get; init; }

    /// <summary>The text of the paragraph.</summary>
    [VectorStoreData]
    public required string Text { get; init; }

    /// <summary>The embedding generated from the Text.</summary>
    [VectorStoreVector(1536)]
    public Embedding<float>? TextEmbedding { get; set; }
}

sealed class TextChunkTextSearchStringMapper : ITextSearchStringMapper
{
    /// <inheritdoc />
    public string MapFromResultToString(object result)
    {
        if (result is TextChunk dataModel)
        {
            return dataModel.Text;
        }
        throw new ArgumentException("Invalid result type.");
    }
}

sealed class TextChunkTextSearchResultMapper : ITextSearchResultMapper
{
    /// <inheritdoc />
    public TextSearchResult MapFromResultToTextSearchResult(object result)
    {
        if (result is TextChunk dataModel)
        {
            return new (value: dataModel.Text) { Name = dataModel.Key, Link = dataModel.DocumentName };
        }
        throw new ArgumentException("Invalid result type.");
    }
}
