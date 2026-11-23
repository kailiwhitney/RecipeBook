using System.ComponentModel.DataAnnotations.Schema;

namespace RecipeBook.Models;

// Not a table: a bag of things a single page needs at once.
[NotMapped]
public class MyViewModel
{
    public Recipe? Recipe { get; set; }
    public List<Recipe> AllRecipes { get; set; } = new();

    public MealPlan? MealPlan { get; set; }

    public Meal? Meal { get; set; }
    public List<Meal> AllMeals { get; set; } = new();

    // Replaces the old DoW int, which assumed every plan was one fixed week.
    public DateOnly SelectedDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
}
