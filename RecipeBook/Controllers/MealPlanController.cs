using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using RecipeBook.Models;

namespace RecipeBook.Controllers;

public class MealPlanController : Controller
{
    private MyContext _context;

    public MealPlanController(MyContext context)
    {
        _context = context;
    }

    // Single place for the current user id. Every read/write below filters on it,
    // because ids arrive from URLs and forms and cannot be trusted to be the
    // caller's own rows.
    private int Uid => HttpContext.Session.GetInt32("uid") ?? 0;

    //! CREATE
    // *** new mealplan ***
    [HttpPost("mealplan/add")]
    public IActionResult CreateMealPlan(MealPlan mp){
        if(ModelState.IsValid){
            // Owner always comes from the session, never from the posted form.
            mp.UserId = Uid;
            // Strip ids a caller might have posted so EF can't hijack an existing row.
            mp.Id = 0;
            mp.ShoppingListId = null;
            _context.MealPlans.Add(mp);
            _context.SaveChanges();
            return Redirect($"/mealplan/{mp.Id}/edit");
        }
        return Redirect("/mealplan");
    }

    //! READ
    // *** view all meal plans ***
    [HttpGet("/mealplan")]
    public IActionResult MealPlans(){
        List<MealPlan> mealPlans = _context.MealPlans
                                    .Where(i=>i.UserId == Uid)
                                    .Include(i=>i.Meals)
                                    .ToList();
        // Counts for the shared right-rail section tabs (Views/Shared/_SectionTabs.cshtml).
        ViewBag.CategoryCounts = GetCategoryCounts();
        return View(mealPlans);
    }

    // Same helper RecipeController uses, duplicated here so each controller stays
    // self-contained. Cheap: no includes.
    private Dictionary<string, int> GetCategoryCounts()
    {
        int uid = Uid;
        var counts = new Dictionary<string, int>();
        foreach (Recipe r in _context.Recipes.Where(r => r.UserId == uid).ToList())
        {
            foreach (RecipeCategory c in r.Categories)
            {
                string key = c.ToString();
                counts.TryGetValue(key, out int n);
                counts[key] = n + 1;
            }
        }
        return counts;
    }

    //! UPDATE
    // *** edit specific meal plan ***
    [HttpGet("mealplan/{mealplanId}/edit")]
    public IActionResult EditMP(int mealplanId){
        // Ownership is part of the lookup, not an after-the-fact check: a foreign
        // id simply resolves to null and falls through to the list.
        MealPlan? item = _context.MealPlans
                        .Include(mp => mp.Meals)
                            .ThenInclude(m => m.MealRecipes)
                            .ThenInclude(j => j.Recipe)
                        .FirstOrDefault(i=>i.Id == mealplanId && i.UserId == Uid);
        if(item == null){
            return RedirectToAction("MealPlans");
        }
        DateOnly startDate = item.StartDate;
        MyViewModel model = new MyViewModel{
            MealPlan = item,
            SelectedDate = startDate,
            AllRecipes = _context.Recipes
                        .Where(i=>i.UserId == Uid)
                        .Include(r => r.Ingredients)
                        .ToList(),
            AllMeals = item.Meals
        };

        // setting viewbags to use for the seven day columns of the grid
        ViewBag.D0 = startDate;
        ViewBag.D1 = startDate.AddDays(1);
        ViewBag.D2 = startDate.AddDays(2);
        ViewBag.D3 = startDate.AddDays(3);
        ViewBag.D4 = startDate.AddDays(4);
        ViewBag.D5 = startDate.AddDays(5);
        ViewBag.D6 = startDate.AddDays(6);

        return View(model);
    }

    [HttpPost("mealplan/{mealPlanId}/update")]
    public IActionResult UpdateMP(MealPlan mp, int mealPlanId){
        if(ModelState.IsValid){
            MealPlan? item = _context.MealPlans
                            .FirstOrDefault(i => i.Id == mealPlanId && i.UserId == Uid);
            // Null check: the old code dereferenced this directly, so a bad or
            // foreign id threw a 500 instead of bouncing the user out.
            if(item == null){
                return RedirectToAction("MealPlans");
            }
            // Only the two user-editable fields are copied, so a crafted post
            // cannot move the plan to another owner or another shopping list.
            item.Name = mp.Name;
            item.StartDate = mp.StartDate;
            _context.MealPlans.Update(item);
            _context.SaveChanges();
            return Redirect("edit");
        }
        return RedirectToAction("MealPlans");
    }

    //! DELETE
    [HttpPost("mealplan/{mealPlanId}/delete")]
    public IActionResult DeleteMP(int mealPlanId){
        MealPlan? itemToDelete = _context.MealPlans
                                .Include(i=>i.Meals)
                                    .ThenInclude(m => m.MealRecipes)
                                .SingleOrDefault(i=>i.Id == mealPlanId && i.UserId == Uid);
        if(itemToDelete != null){
            // delete the recipe links first, then the meal slots
            foreach(Meal m in itemToDelete.Meals){
                foreach(MealRecipe link in m.MealRecipes){
                    _context.MealRecipes.Remove(link);
                }
                _context.Meals.Remove(m);
            }
            // delete meal plan
            _context.MealPlans.Remove(itemToDelete);
            _context.SaveChanges();
        }
        return Redirect("/mealplan");
    }
}
