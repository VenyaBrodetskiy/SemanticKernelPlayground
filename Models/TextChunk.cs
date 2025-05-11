using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;

namespace SemanticKernelPlayground.Models;

public record TextChunk
{
    /// <summary>A unique key for the text paragraph.</summary>
    [VectorStoreRecordKey]
    public required string Key { get; init; }

    /// <summary>A name that points at the original location of the document containing the text.</summary>
    [VectorStoreRecordData]
    public required string DocumentName { get; init; }

    /// <summary>The id of the paragraph from the document containing the text.</summary>
    [VectorStoreRecordData]
    public required int ParagraphId { get; init; }

    /// <summary>The text of the paragraph.</summary>
    [VectorStoreRecordData]
    public required string Text { get; init; }

    /// <summary>The embedding generated from the Text.</summary>
    [VectorStoreRecordVector(1536)]
    public ReadOnlyMemory<float> TextEmbedding { get; set; }
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
