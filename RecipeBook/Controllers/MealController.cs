using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeBook.Models;

namespace RecipeBook.Controllers;

public class MealController : Controller
{
    private MyContext _context;

    public MealController(MyContext context)
    {
        _context = context;
    }

    private int Uid => HttpContext.Session.GetInt32("uid") ?? 0;

    // Resolve a meal slot only if it belongs to a plan the caller owns. Returning
    // null (instead of throwing) lets every caller fall through to a redirect, so a
    // foreign mealId behaves like "not found" rather than leaking or mutating data.
    private Meal? FindOwnedMeal(int mealId)
    {
        return _context.Meals
            .Include(m => m.MealRecipes)
            .Include(m => m.MealPlan)
            .SingleOrDefault(m => m.Id == mealId && m.MealPlan.UserId == Uid);
    }

    // A meal is one (date, type) slot in a plan. Adding the same slot twice is a
    // no-op that returns the existing meal, so the unique (plan, date, type) index
    // is never violated and "add recipe" can always target a real meal.
    // Returns null when the plan is not the caller's, which blocks planting
    // recipes into somebody else's plan.
    private Meal? GetOrCreateMeal(int mealPlanId, DateOnly date, MealType type)
    {
        MealPlan? plan = _context.MealPlans
            .FirstOrDefault(p => p.Id == mealPlanId && p.UserId == Uid);
        if (plan == null)
        {
            return null;
        }

        Meal? meal = _context.Meals.SingleOrDefault(m =>
            m.MealPlanId == mealPlanId && m.Date == date && m.Type == type);
        if (meal == null)
        {
            meal = new Meal { MealPlanId = mealPlanId, Date = date, Type = type };
            _context.Meals.Add(meal);
            _context.SaveChanges();
        }
        return meal;
    }

    //! CREATE
    // *** add one recipe into a (date, type) slot ***
    [HttpPost("meal/{mealPlanId}/add")]
    public IActionResult AddRecipe(int mealPlanId, DateOnly date, MealType type, int recipeId)
    {
        Meal? meal = GetOrCreateMeal(mealPlanId, date, type);
        if (meal == null)
        {
            return Redirect("/mealplan");
        }

        // The recipe must also be the caller's own, or a foreign recipe id could be
        // referenced here and surfaced later through the plan view.
        bool recipeOwned = _context.Recipes.Any(r => r.Id == recipeId && r.UserId == Uid);
        if (!recipeOwned)
        {
            return Redirect($"/mealplan/{mealPlanId}/edit");
        }

        bool alreadyIn = _context.MealRecipes.Any(j => j.MealId == meal.Id && j.RecipeId == recipeId);
        if (!alreadyIn)
        {
            _context.MealRecipes.Add(new MealRecipe { MealId = meal.Id, RecipeId = recipeId });
            _context.SaveChanges();
        }
        return Redirect($"/mealplan/{mealPlanId}/edit");
    }

    //! UPDATE — rename/retype a slot is not modelled yet; only membership changes.

    //! DELETE
    // *** remove one recipe from a meal; drop the slot if it becomes empty ***
    [HttpPost("meal/{mealId}/recipe/{recipeId}/remove")]
    public IActionResult RemoveRecipe(int mealId, int recipeId)
    {
        Meal? meal = FindOwnedMeal(mealId);
        if (meal == null)
        {
            return Redirect("/mealplan");
        }

        MealRecipe? link = _context.MealRecipes
            .SingleOrDefault(j => j.MealId == mealId && j.RecipeId == recipeId);
        if (link != null)
        {
            _context.MealRecipes.Remove(link);
            // An empty slot carries no information, so it goes with its last recipe.
            bool anyLeft = _context.MealRecipes.Any(j => j.MealId == mealId);
            if (!anyLeft)
            {
                _context.Meals.Remove(meal);
            }
            _context.SaveChanges();
        }
        return Redirect($"/mealplan/{meal.MealPlanId}/edit");
    }

    // *** delete a whole meal slot and its recipe links ***
    [HttpPost("meal/{mealId}/delete")]
    public IActionResult DeleteMeal(int mealId)
    {
        Meal? meal = FindOwnedMeal(mealId);
        if (meal != null)
        {
            int mpId = meal.MealPlanId;
            foreach (MealRecipe link in meal.MealRecipes.ToList())
            {
                _context.MealRecipes.Remove(link);
            }
            _context.Meals.Remove(meal);
            _context.SaveChanges();
            return Redirect($"/mealplan/{mpId}/edit");
        }
        return Redirect("/mealplan");
    }
}
