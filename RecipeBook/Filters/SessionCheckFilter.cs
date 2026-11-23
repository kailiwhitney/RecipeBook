using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace RecipeBook.Filters;

// Global auth guard: any request whose session has no uid goes back to the
// login page, regardless of which page was requested. Registered app-wide in
// Program.cs via MvcOptions.Filters + TypeFilter (registering it as
// IFilterMetadata in DI does nothing). Only the auth endpoints themselves are
// exempt (login form, register, login, logout) plus Error so exceptions can
// still render. Action names are matched case-insensitively because the
// attribute routes ("login", "register") are lowercase in the views.
//
// Note: this only redirects; it does not preserve a deep-link target — an
// unauthenticated visit to /recipe/42 lands on the login page, and after login
// goes to the recipe book. Say the word if you want return-URL support.
//
// Redirect targets: no session -> User.Index (the login form, exempt);
// logged in on an auth page -> Recipe.Index (the app home at /recipes).
//
// This guard is also what keeps the chrome honest: _Layout only renders the
// family title and the global action buttons when a session exists, so a lost
// session used to render a button-less page instead of sending you to login.
public class SessionCheckFilter : ActionFilterAttribute
{
    private static readonly HashSet<(string, string)> Exempt = new()
    {
        ("user", "index"),
        ("user", "register"),
        ("user", "login"),
        ("user", "logout"),
    };

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var desc = context.ActionDescriptor as ControllerActionDescriptor;
        string controller = desc?.ControllerName ?? "";
        string action = desc?.ActionName ?? "";
        if (action == "Error" || Exempt.Contains((controller.ToLowerInvariant(), action.ToLowerInvariant())))
        {
            return;
        }

        int? uid = context.HttpContext.Session.GetInt32("uid");
        if (uid == null)
        {
            context.Result = new RedirectToActionResult("Index", "User", null);
        }
        else if (controller.Equals("User", System.StringComparison.OrdinalIgnoreCase))
        {
            // Logged-in user poking at auth pages: send them to the app home,
            // which is the recipes list (RecipeController.Index).
            context.Result = new RedirectToActionResult("Index", "Recipe", null);
        }
    }
}
