using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeBook.Models;

namespace RecipeBook.Controllers;

public class ShoppingListController : Controller
{
    private MyContext _context;

    public ShoppingListController(MyContext context)
    {
        _context = context;
    }

    // Each user has exactly one shopping list, created lazily on first open so
    // there is no separate create step and no empty-list pileup. NeededIngredients
    // is computed (not stored), so it is filled after the list exists.
    private ShoppingList GetOrCreateUserList(int uid)
    {
        ShoppingList? sl = _context.ShoppingLists
                            .Include(i => i.MealPlans)
                            .FirstOrDefault(i => i.UserId == uid);
        if (sl == null)
        {
            sl = new ShoppingList { Name = "Shopping List", UserId = uid };
            _context.ShoppingLists.Add(sl);
            _context.SaveChanges();
        }
        return sl;
    }

    // Fill the transient needs + cost from every plan currently linked to this list.
    private ShoppingList WithNeeds(ShoppingList sl)
    {
        List<int> mpIds = _context.MealPlans
                            .Where(mp => mp.ShoppingListId == sl.Id)
                            .Select(mp => mp.Id)
                            .ToList();
        sl.NeededIngredients = PlanningCalculator.NeededIngredients(_context, mpIds);
        return sl;
    }

    //! READ — the canonical URL for the shopping list screen.
    [HttpGet("/shoppinglist")]
    public IActionResult Index()
    {
        int uid = HttpContext.Session.GetInt32("uid") ?? 0;
        ShoppingList sl = WithNeeds(GetOrCreateUserList(uid));
        return View("viewSL", sl);
    }

    // Legacy deep link kept so old bookmarks still work: forward to the user's list.
    [HttpGet("/shoppingList/{slId}/view")]
    public IActionResult ViewSL(int slId) => Redirect("/shoppinglist");

    //! UPDATE — attach a plan to this list and refresh the aggregated needs.
    // Attaching is idempotent; re-running after recipe edits just recomputes needs,
    // which is why nothing needs "clear existing products" any more.
    [HttpGet("mealplan/{mpId}/shoppingList/generate")]
    public IActionResult GenerateFromPlan(int mpId)
    {
        int uid = HttpContext.Session.GetInt32("uid") ?? 0;
        ShoppingList sl = GetOrCreateUserList(uid);

        MealPlan? mp = _context.MealPlans
                        .FirstOrDefault(i => i.Id == mpId && i.UserId == uid);
        if (mp != null && mp.ShoppingListId != sl.Id)
        {
            mp.ShoppingListId = sl.Id;
            _context.SaveChanges();
        }
        return Redirect("/shoppinglist");
    }

    //! DELETE — detach a plan from the list (the plan itself is untouched).
    [HttpPost("mealplan/{mpId}/shoppingList/detach")]
    public IActionResult DetachPlan(int mpId)
    {
        int uid = HttpContext.Session.GetInt32("uid") ?? 0;
        MealPlan? mp = _context.MealPlans
                        .FirstOrDefault(i => i.Id == mpId && i.UserId == uid);
        if (mp != null)
        {
            mp.ShoppingListId = null;
            _context.SaveChanges();
        }
        return Redirect("/shoppinglist");
    }
}
