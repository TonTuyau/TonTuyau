using System.Linq.Expressions;
using Elastic.Clients.Elasticsearch;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Elasticsearch.Documents;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>What kind of value a sort key puts in a position (and accepts back from a cursor).</summary>
internal enum SortKeyKind
{
    /// <summary><c>_score</c>: a real number.</summary>
    Score,

    /// <summary>A number or a date (Elasticsearch gives dates as epoch milliseconds).</summary>
    Integer,

    /// <summary>A keyword.</summary>
    Text,
}

/// <summary>One key of a list's sort. Every list ends with the id, so the order is total and pages never overlap.</summary>
internal sealed record SortKey(SortOptions Options, SortKeyKind Kind)
{
    public static SortKey Score() =>
        new(new SortOptions { Score = new ScoreSort { Order = SortOrder.Desc } }, SortKeyKind.Score);

    public static SortKey By<TDocument>(
        Expression<Func<TDocument, object?>> field,
        SortOrder order,
        SortKeyKind kind) =>
        new(new SortOptions { Field = new FieldSort { Field = Infer.Field(field), Order = order } }, kind);

    public static SortKey Id<TDocument>()
        where TDocument : IStoreDocument =>
        By<TDocument>(d => d.Id, SortOrder.Asc, SortKeyKind.Text);
}

/// <summary>Converts between Elasticsearch's sort values and the store's <see cref="SortPosition"/>.</summary>
internal static class SortPositions
{
    public static SortPosition From(IReadOnlyCollection<FieldValue>? values)
    {
        if (values is null || values.Count == 0)
        {
            throw new SearchStoreException("A sorted search hit came back without sort values.");
        }

        return new SortPosition(
        [
            .. values.Select(v => v switch
            {
                { IsString: true } => new TextSortValue((string)v.Value!),
                { IsLong: true } => new IntegerSortValue((long)v.Value!),
                { IsDouble: true } => (SortValue)new RealSortValue((double)v.Value!),
                _ => throw new SearchStoreException($"Unexpected sort value kind {v.Kind} from Elasticsearch."),
            }),
        ]);
    }

    /// <exception cref="Application.Validation.ValidationException">
    /// The position doesn't fit the sort (a cursor of another list or a hand-made one).
    /// </exception>
    public static ICollection<FieldValue> ToSearchAfter(SortPosition position, IReadOnlyList<SortKey> keys)
    {
        if (position.Values.Count != keys.Count)
        {
            throw CursorCodec.Invalid();
        }

        return
        [
            .. position.Values.Zip(
                keys,
                (value, key) => (value, key.Kind) switch
                {
                    (TextSortValue text, SortKeyKind.Text) => FieldValue.String(text.Value),
                    (IntegerSortValue integer, SortKeyKind.Integer or SortKeyKind.Score) =>
                        FieldValue.Long(integer.Value),
                    (RealSortValue real, SortKeyKind.Score) => FieldValue.Double(real.Value),
                    _ => throw CursorCodec.Invalid(),
                }),
        ];
    }
}
