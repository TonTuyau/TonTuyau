using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using TonTuyau.Application.Store;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>Where the last word being typed may be the start of a word, and how much a match there weighs.</summary>
/// <param name="Weight">The field's weight (as for whole words); the query's boost is this × <see cref="SearchTextQueries.PrefixBoost"/>.</param>
/// <param name="IndexedPrefixes">
/// True for a field mapped with <c>index_prefixes</c> (descriptions, tags: long text, QA11 M5-13): a
/// <c>match_bool_prefix</c>, answered from the indexed prefixes up to 5 characters, from the term dictionary beyond.
/// False for an <c>.autocomplete</c> edge n-gram sub-field (titles, names): a scored <c>match</c>.
/// </param>
internal sealed record PrefixField(Field Field, float Weight, bool IndexedPrefixes = false);

/// <summary>
/// The typed queries of a search's text (QA10 M5-1, QA11 M5-13). Every word must be found (AND, M4-Q1), each in any
/// field (cross fields: "tom huile" finds Tom Cooks' oil video). The last word, while still being typed, matches either
/// as a whole word in any field or as the start of a word in any field too (accents ignored). A whole-word match also
/// matches the start-of-word clause of its field, so in one field it always scores above a start-only match.
/// </summary>
internal static class SearchTextQueries
{
    /// <summary>Weight of a start-of-word match of the last word, below a whole-word match.</summary>
    public const float PrefixBoost = 0.5f;

    /// <param name="wholeFields">The analysed fields searched for whole words (with their boosts).</param>
    /// <param name="prefixFields">
    /// The fields searched for the start of the last word, with their weights (a match query takes no
    /// <c>field^boost</c>: the weight goes on the query, times <see cref="PrefixBoost"/>).
    /// </param>
    public static Query[] Must(SearchText text, Field[] wholeFields, IReadOnlyList<PrefixField> prefixFields)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.IsEmpty)
        {
            return [new MatchNoneQuery()];
        }

        List<Query> must = [];
        if (text.Words.Count > 0)
        {
            must.Add(WholeWords(string.Join(' ', text.Words), wholeFields));
        }

        if (text.Prefix is not null)
        {
            must.Add(new BoolQuery
            {
                Should =
                [
                    WholeWords(text.Prefix, wholeFields),
                    .. prefixFields.Select(f => StartOfWord(f, text.Prefix)),
                ],
                MinimumShouldMatch = 1,
            });
        }

        return [.. must];
    }

    private static MultiMatchQuery WholeWords(string words, Field[] fields) => new()
    {
        Query = words,
        Fields = fields,
        Type = TextQueryType.CrossFields,
        Operator = Operator.And,
    };

    private static Query StartOfWord(PrefixField field, string prefix) => field.IndexedPrefixes
        ? new MatchBoolPrefixQuery
        {
            Field = field.Field,
            Query = prefix,
            Operator = Operator.And,
            Boost = PrefixBoost * field.Weight,
        }
        : new MatchQuery
        {
            Field = field.Field,
            Query = prefix,
            Operator = Operator.And,
            Boost = PrefixBoost * field.Weight,
        };
}
