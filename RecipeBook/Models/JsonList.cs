using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RecipeBook.Models;

// EF Core 9 added Property(x => x.SomeList).ToJson(). This project is on EF Core 8
// (Pomelo 8), where that API does not exist (it was the RZ/CS1929 build failure), so
// the List<T> properties on Recipe are persisted the orthodox EF8 way: a value
// converter that serializes the list to a string column and parses it back. MyContext
// wires these up next to each Property(...) call.
//
// Change-tracking caveat: EF snapshots the materialized list by reference, so
// in-place mutations (recipe.Tags.Add("x")) may not be seen as a change. Assigning a
// whole new list is always safe, and every current call site assigns.

public static class JsonList
{
    // Enums serialize to their numeric value by default, which is what we want:
    // renaming an enum member (we renamed one already) will not corrupt stored rows.
    public static string Write<T>(List<T>? items)
    {
        return JsonSerializer.Serialize(items ?? new List<T>());
    }

    // Tolerates null/empty/historical garbage: a bad column yields an empty list
    // rather than throwing during page rendering.
    public static List<T> Read<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<T>();
        }
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
    }
}
