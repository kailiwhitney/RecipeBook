using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeBook.Models;

namespace RecipeBook.Controllers;

public class RecipeController : Controller
{
    private MyContext _context;

    public RecipeController(MyContext context)
    {
        _context = context;
    }

    private int Uid => HttpContext.Session.GetInt32("uid") ?? 0;

    // "recipes" is the app home. The root URL "/" is NOT mapped here: it belongs
    // to UserController.Index, which bounces an authenticated visitor back here
    // via RedirectToResult("Index","Recipe") -> "/recipes". Mapping "/" here as
    // well made it a second, equally-ranked candidate for the root URL (an empty
    // attribute template and "/" are the same template to the router), which is
    // what produced the AmbiguousMatchException on every request to "/".
    [HttpGet("recipes")]
    public IActionResult Index(){
        ViewBag.CategoryCounts = GetCategoryCounts();
        SetTagPickLists();

        // Cover stats (the three numbers under the title). Moved here from
        // HomeController.Dashboard when that page was merged into this view.
        ViewBag.RecipeCount = _context.Recipes.Count(r => r.UserId == Uid);
        ViewBag.FavoriteCount = _context.Recipes.Count(r => r.UserId == Uid && r.Favorite);
        ViewBag.MealPlanCount = _context.MealPlans.Count(m => m.UserId == Uid);

        // index.cshtml is typed @model MyViewModel and enumerates Model.AllRecipes,
        // so returning a bare View() left Model null (NullReferenceException on the
        // home page). Ingredients are included because the table counts them.
        MyViewModel vm = new()
        {
            AllRecipes = _context.Recipes
                            .Where(r => r.UserId == Uid)
                            .Include(i => i.Ingredients)
                            .ToList()
        };
        return View(vm);
    }

    private void SetTagPickLists()
    {
        int uid = Uid;
        var recipes = _context.Recipes.Where(r => r.UserId == uid).ToList();
        ViewBag.AllTags = TagNormalizer.CollectUsed(recipes.Select(r => r.Tags));
        ViewBag.AllCuisine = TagNormalizer.CollectUsed(recipes.Select(r => r.Cuisine));
    }

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

    //! CREATE
    // *** new recipe ***
    [HttpPost("recipe/add")]
    public IActionResult CreateRecipe(Recipe recipe){
        if(ModelState.IsValid){
            // Owner comes from the session only, and the id is cleared so a posted
            // primary key cannot overwrite an existing row.
            recipe.UserId = Uid;
            recipe.Id = 0;
            recipe.Tags = TagNormalizer.NormalizeList(recipe.Tags);
            recipe.Cuisine = TagNormalizer.NormalizeList(recipe.Cuisine);
            _context.Recipes.Add(recipe);
            _context.SaveChanges();
            return Redirect($"{recipe.Id}/edit");
        }
        string back = recipe.Categories.Count > 0 ? recipe.Categories[0].ToString() : "recipes";
        return Redirect($"/recipes/{back}");
    }

    //! READ
    // **** view all recipes in a category ****
    [HttpGet("recipes/{category}")]
    public IActionResult ViewCategory(string category){
        if (!Enum.TryParse<RecipeCategory>(category, ignoreCase: true, out RecipeCategory cat))
        {
            return RedirectToAction("Index");
        }
        List<Recipe> recipes = _context.Recipes
                .Where(r => r.UserId == Uid)
                .Include(i=>i.Ingredients)
                .ToList()
                .Where(r => r.Categories.Contains(cat))
                .ToList();
        ViewBag.Category = category;
        ViewBag.CategoryCounts = GetCategoryCounts();
        SetTagPickLists();
        return View(recipes);
    }

    // *** view single recipe ***
    [HttpGet("/recipe/{recipeId}/view")]
    public IActionResult ViewRecipe(int recipeId){
        Recipe? item = _context.Recipes
                        .Include(i => i.Ingredients)
                        .FirstOrDefault(i => i.Id == recipeId && i.UserId == Uid);
        if(item == null){
            return RedirectToAction("Index");
        }
        return View(item);
    }

    //! UPDATE
    // *** edit a recipe ***
    [HttpGet("recipe/{recipeId}/edit")]
    public IActionResult EditRecipe(int recipeId){
        Recipe? item = _context.Recipes
                        .Include(i => i.Ingredients)
                        .FirstOrDefault(i => i.Id == recipeId && i.UserId == Uid);
        if(item == null){
            return RedirectToAction("Index");
        }
        SetTagPickLists();
        return View(item);
    }

    [HttpPost("recipe/{recipeId}/update")]
    public IActionResult UpdateRecipe(Recipe recipe, int recipeId){
        if(ModelState.IsValid){
            Recipe? item = _context.Recipes
                            .FirstOrDefault(i => i.Id == recipeId && i.UserId == Uid);
            // Null check replaces the previous direct dereference, which threw a
            // 500 whenever the id was missing or belonged to someone else.
            if(item == null){
                return RedirectToAction("Index");
            }
            // Field-by-field copy on purpose: UserId and the navigation collections
            // are deliberately never taken from the posted form.
            item.Url = recipe.Url;
            item.Title = recipe.Title;
            item.Categories = recipe.Categories;
            item.Yield = recipe.Yield;
            item.PrepTime = recipe.PrepTime;
            item.CookTime = recipe.CookTime;
            item.Difficulty = recipe.Difficulty;
            item.Rating = recipe.Rating;
            item.Calories = recipe.Calories;
            item.Favorite = recipe.Favorite;
            item.Directions = recipe.Directions;
            item.Equipment = recipe.Equipment;
            item.Tags = TagNormalizer.NormalizeList(recipe.Tags);
            item.Cuisine = TagNormalizer.NormalizeList(recipe.Cuisine);
            _context.Recipes.Update(item);
            _context.SaveChanges();
        }
        return Redirect("edit");
    }

    //! DELETE
    // *** remove a recipe ***
    [HttpPost("recipe/{recipeId}/delete")]
    public IActionResult DeleteRecipe(int recipeId){
        Recipe? itemToDelete = _context.Recipes
                                .Include(i=>i.Ingredients)
                                .SingleOrDefault(i=>i.Id == recipeId && i.UserId == Uid);
        if(itemToDelete != null){
            // delete all ingredients
            foreach(Ingredient i in itemToDelete.Ingredients){
                _context.Ingredients.Remove(i);
            }
            // delete all recipe links in meals, then the meals they belonged to
            List<MealRecipe> links = _context.MealRecipes
                                    .Where(j => j.RecipeId == recipeId)
                                    .ToList();
            foreach(MealRecipe link in links){
                _context.MealRecipes.Remove(link);
            }
            // an emptied meal slot carries nothing, so drop those too
            List<int> touchedMealIds = links.Select(l => l.MealId).Distinct().ToList();
            foreach(int mealId in touchedMealIds){
                bool anyLeft = _context.MealRecipes.Any(j => j.MealId == mealId);
                if(!anyLeft){
                    Meal? empty = _context.Meals.SingleOrDefault(m => m.Id == mealId);
                    if(empty != null){
                        _context.Meals.Remove(empty);
                    }
                }
            }
            // delete recipe
            _context.Recipes.Remove(itemToDelete);
            _context.SaveChanges();
        }
        return Redirect("/recipes");
    }
}
