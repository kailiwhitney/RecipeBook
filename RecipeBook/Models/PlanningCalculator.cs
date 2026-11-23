using Microsoft.EntityFrameworkCore;
using RecipeBook.Models;

namespace RecipeBook.Models;

// Aggregates what to buy for a set of meal plans. Deliberately a pure function over
// the DbContext rather than a stored table: the needs are 100% derivable from the
// linked plans' recipes, so storing them would only create a thing that goes stale
// whenever a recipe is edited. Recompute on read; add a cache row only if profiling
// ever shows this to be hot.

public static class PlanningCalculator
{
    // Sum Ingredient amounts across every recipe in the given plans, grouped by
    // (Name, Unit). Units are never converted — matching "2 lb" with "2 kg" would be
    // wrong — so the same name can appear on more than one row if recipes spell the
    // unit differently. That is a data-entry concern, not something to guess here.
    public static List<IngredientNeed> NeededIngredients(MyContext db, List<int> mealPlanIds)
    {
        if (mealPlanIds.Count == 0)
        {
            return new List<IngredientNeed>();
        }

        // One query pulls the ingredient rows; grouping happens in memory so the
        // trim/compare rules stay ordinary C# instead of translatable-SQL gymnastics.
        List<Ingredient> ingredients = db.MealPlans
            .Where(mp => mealPlanIds.Contains(mp.Id))
            .SelectMany(mp => mp.Meals)
                .SelectMany(m => m.MealRecipes)
                    .SelectMany(mr => mr.Recipe.Ingredients)
            .ToList();

        return ingredients
            .GroupBy(i => new { Name = i.Name.Trim(), Unit = i.Unit.Trim() })
            .Select(g => new IngredientNeed
            {
                Name = g.Key.Name,
                Unit = g.Key.Unit,
                Quantity = g.Sum(i => i.Amount)
            })
            .OrderBy(n => n.Name)
            .ToList();
    }
}
