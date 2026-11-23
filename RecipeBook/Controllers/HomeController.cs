using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RecipeBook.Models;

namespace RecipeBook.Controllers;

// The former Dashboard action (welcome cover page) was moved into
// RecipeController.Index + Views/Recipe/index.cshtml when "/" and "/recipes"
// were consolidated onto one home page. HomeController now exists only to serve
// the exception handler path (/Home/Error, see UseExceptionHandler in Program.cs).
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
