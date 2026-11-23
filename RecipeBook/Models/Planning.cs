using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace RecipeBook.Models
{
    // A meal plan is a named, dated grid of meals. Each Meal is one slot (a type
    // on one date) and holds the recipes cooked for it.

    public class MealPlan
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateOnly StartDate { get; set; }

        // The shopping list this plan was generated into, if any. Nullable so a
        // plan can exist before it is ever shopped. Gives ShoppingList its
        // "list of meal plans" without a join table.
        public int? ShoppingListId { get; set; }
        public ShoppingList? ShoppingList { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public List<Meal> Meals { get; set; } = new();
    }

    // Replaces PlanEntry. Where a PlanEntry was one recipe in one slot, a Meal is
    // a typed slot (breakfast, lunch, ...) that can hold several recipes at once.
    // Recipes attach through the Meal.Recipes many-to-many join, so the same dish
    // can appear in many meals and a meal can combine many dishes.

    public class Meal
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public MealType Type { get; set; } = MealType.Dinner;

        // Kilocalories for this meal: the sum of its recipes' Calories. Not a column,
        // so it can never drift from the recipes it is computed from. Read-only, so
        // the model binder never tries to set it from a form post.
        [NotMapped]
        public int MealCalories => MealRecipes?.Sum(mr => mr.Recipe.Calories) ?? 0;

        public int MealPlanId { get; set; }
        public MealPlan MealPlan { get; set; } = null!;

        // The recipes in this meal. Many-to-many with Recipe through the explicit
        // MealRecipe join class below.
        public List<MealRecipe> MealRecipes { get; set; } = new();
    }

    // Explicit join row for Meal <-> Recipe. Deliberately not two bare collection
    // navigations: with only those, EF creates a shadow join table it will drop and
    // rebuild whenever the owning entity is reloaded, silently wiping meal slots.
    // A real class also gives the pairing an Id, so one recipe can be removed from
    // one meal without touching any other.

    public class MealRecipe
    {
        public int Id { get; set; }

        public int MealId { get; set; }
        public Meal Meal { get; set; } = null!;

        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;
    }

    public enum MealType
    {
        Breakfast,
        Lunch,
        Dinner,
        Snack,
        Dessert,
        Drink
    }

    // Owned by the user. "list of meal plans" is a one-to-many (one list generated
    // from several plans), so MealPlan gains a nullable ShoppingListId back-FK —
    // EF can't infer an owner from a bare List<MealPlan>. EstimatedCost is manual
    // for now: no price source exists yet, so nothing computes it.

    public class ShoppingList
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateOnly? TargetDate { get; set; }

        // Placeholder for the eventual cost estimate (sum over needed ingredients).
        // Stays 0 until there is a price source. double per spec.
        public double EstimatedCost { get; set; } = 0;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // The meal plans this list was generated from. Backed by MealPlan.ShoppingListId.
        public List<MealPlan> MealPlans { get; set; } = new();

        // Needed ingredients, computed on demand from the linked plans' recipes.
        // Not stored (see PlanningCalculator.NeededIngredients). Recomputed each read;
        // move to a cache table only if profiling says the recompute is hot.
        [NotMapped]
        public List<IngredientNeed> NeededIngredients { get; set; } = new();
    }

    // A single aggregated shopping need: "buy this much of this unit". Produced by
    // summing matching Ingredient lines across every recipe in the linked plans.
    // Not an entity — a transient projection, so it has no Id and is never mapped.

    public class IngredientNeed
    {
        public string Name { get; set; } = "";
        public float Quantity { get; set; }
        public string Unit { get; set; } = "";
    }
}
