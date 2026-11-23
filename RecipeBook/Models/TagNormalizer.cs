using System.Collections.Generic;

namespace RecipeBook.Models;

/// <summary>
/// Shared normalization for the "dynamic enum" string lists (Tags, Cuisine).
/// These are not real C# enums - users create values freely - so the only way to
/// guarantee camping == Camping is to funnel every write through here. The stored
/// form uses title-case ("Camping"); comparison is case-insensitive and trims,
/// and duplicates collapse. Display sites just show whatever was stored.
/// </summary>
public static class TagNormalizer
{
    /// Normalize one raw input into its canonical stored form.
    /// Returns null for blank/whitespace so callers can skip empties.
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Collapse internal runs of whitespace to a single space, then title-case
        // each word ("camping" -> "Camping", "panang curry" -> "Panang Curry").
        var trimmed = string.Join(' ', raw.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));

        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trimmed);
    }

    /// Normalize a whole list: drop blanks, dedupe case-insensitively,
    /// preserve first-seen order. Used on every save path.
    public static List<string> NormalizeList(IEnumerable<string>? raw)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        if (raw == null) return result;

        foreach (var item in raw)
        {
            string? norm = Normalize(item);
            if (norm == null) continue;
            if (seen.Add(norm)) result.Add(norm);
        }
        return result;
    }

    /// Distinct, sorted set of every value used across all recipes.
    /// Feeds the badge pick-lists so an existing tag can be reused on another
    /// recipe.
    public static List<string> CollectUsed(IEnumerable<IEnumerable<string>?> allLists)
    {
        var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in allLists)
            foreach (var v in NormalizeList(list))
                set.Add(v);
        return set.ToList();
    }
}
