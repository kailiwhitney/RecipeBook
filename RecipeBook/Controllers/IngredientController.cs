using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeBook.Models;

namespace RecipeBook.Controllers;

public class IngredientController : Controller
{
    private readonly ILogger<IngredientController> _logger;
    private MyContext _context;

    public IngredientController(ILogger<IngredientController> logger, MyContext context)
    {
        _logger = logger;
        _context = context;
    }

    private int Uid => HttpContext.Session.GetInt32("uid") ?? 0;

    //! CREATE
    // *** new ingredient to recipe ingredient list ***
    [HttpPost("addIngredient")]
    public IActionResult AddIngredient(Ingredient ingredient){
        // Debug output now goes through the logging pipeline: Console.WriteLine
        // bypasses log levels and sinks, and this wrote a whole row per request.
        _logger.LogDebug("AddIngredient valid={Valid} name={Name} amount={Amount} unit={Unit}",
            ModelState.IsValid, ingredient.Name, ingredient.Amount, ingredient.Unit);

        if(ModelState.IsValid){
            // The parent recipe must belong to the caller, otherwise an arbitrary
            // RecipeId from the form would graft an ingredient onto a stranger's
            // recipe. Id is cleared for the same reason it is on create elsewhere.
            bool recipeOwned = _context.Recipes.Any(r => r.Id == ingredient.RecipeId && r.UserId == Uid);
            if(!recipeOwned){
                return RedirectToAction("Index", "Recipe");
            }
            ingredient.Id = 0;
            _context.Ingredients.Add(ingredient);
            _context.SaveChanges();
        }
        return Redirect($"recipe/{ingredient.RecipeId}/edit");
    }

    //! READ

    //! UPDATE

    //! DELETE
    // *** remove ingredient from recipe list of ingredients ***
    [HttpPost("{ingredientId}/deleteIngredient")]
    public IActionResult DeleteIngredient(int ingredientId){
        // Walk to the owner through the recipe so only the creator can delete.
        Ingredient? itemToDelete = _context.Ingredients
            .Include(i => i.Recipe)
            .SingleOrDefault(i => i.Id == ingredientId && i.Recipe.UserId == Uid);
        if(itemToDelete != null){
            int recipeId = itemToDelete.RecipeId;
            _context.Ingredients.Remove(itemToDelete);
            _context.SaveChanges();
            return Redirect($"/recipe/{recipeId}/edit");
        }
        return Redirect($"/recipe");
    }
}
