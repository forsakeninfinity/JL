using System.Globalization;
using System.Text;
using JL.Core.Dicts.Options;

namespace JL.Core.Dicts.EDICT.JMnedict;

internal sealed class JmnedictRecord : IDictRecord
{
    public int Id { get; }
    public string PrimarySpelling { get; }
    public string[]? AlternativeSpellings { get; }
    public string[]? Readings { get; }
    public string[][] Definitions { get; }
    public string[][] NameTypes { get; }
    //public string[]?[]? RelatedTerms { get; }

    public JmnedictRecord(int id, string primarySpelling, string[]? alternativeSpellings, string[]? readings, string[][] definitions, string[][] nameTypes)
    {
        Id = id;
        PrimarySpelling = primarySpelling;
        AlternativeSpellings = alternativeSpellings;
        Readings = readings;
        Definitions = definitions;
        NameTypes = nameTypes;
        //RelatedTerms = new List<List<string>?>();
    }

    public string BuildFormattedDefinition(DictOptions? options)
    {
        string separator = options is { NewlineBetweenDefinitions.Value: false }
            ? ""
            : "\n";

        bool multipleDefinitions = Definitions.Length > 1;

        StringBuilder defResult = new();

        for (int i = 0; i < Definitions.Length; i++)
        {
            string[] definitions = Definitions[i];

            if (multipleDefinitions)
            {
                _ = defResult.Append(CultureInfo.InvariantCulture, $"({i + 1}) ");
            }

            if (NameTypes?.Length >= i)
            {
                string[] nameTypes = NameTypes[i];

                if (nameTypes.Length > 1 || !nameTypes.Contains("unclass"))
                {
                    _ = defResult.Append(CultureInfo.InvariantCulture, $"({string.Join(", ", nameTypes)}) ");
                }
            }

            //if (options?.RelatedTerm?.Value ?? false)
            //{
            //    string[]? relatedTerms = RelatedTerms?[i];
            //    if (relatedTerms?.Count > 0)
            //    {
            //        _ = defResult.Append("(related terms: {string.Join(", ", relatedTerms)}) ");
            //    }
            //}

            _ = defResult.Append(CultureInfo.InvariantCulture, $"{string.Join("; ", definitions)} {separator}");
        }

        return defResult.Remove(defResult.Length - separator.Length - 1, separator.Length + 1).ToString();
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        JmnedictRecord jmnedictObj = (JmnedictRecord)obj;
        return Id == jmnedictObj.Id && PrimarySpelling == jmnedictObj.PrimarySpelling;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return Id * PrimarySpelling.GetHashCode(StringComparison.Ordinal);
        }
    }
}
