using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecipeBook.Models
{
    // Ownership root. Recipes, meal plans, and shopping lists all hang off a user.

    // The attributes below are what make ModelState.IsValid meaningful in
    // UserController; without them every "if (ModelState.IsValid)" upstream was
    // trivially true and the [Required]/[EmailAddress] intent existed only in the
    // browser's HTML, which is a convenience and not a control.
    public class User
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = "";

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = "";

        // Family name kept alongside first/last: what the household calls this
        // person. Optional, so nullable and no validation attribute.
        public string? FamilyName { get; set; }

        [Required]
        [Display(Name = "Email")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = "";

        // Login handle. Kept separate from Email so a person can sign in with
        // either. Uniqueness is checked app-side in Register (mirrors the email
        // check) rather than by a DB index.
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = "";

        // Optional contact field: no [Required], may be left blank at sign-up.
        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        // NOTE: profile image intentionally not modelled yet — storage strategy is
        // undecided. When added, prefer a path/URL string column over a byte[] blob
        // so queries that list users never drag image bytes along.
        public string Password { get; set; } = "";

        /// Not a column: only exists so the register form can validate that the
        /// two password boxes match.
        [NotMapped]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        public string ConfirmPW { get; set; } = "";

        // Content this person owns. The delete cascade walks these, so controllers
        // that remove a user must load them (Include) first.
        public List<Recipe> Recipes { get; set; } = new();
        public List<MealPlan> MealPlans { get; set; } = new();
        public List<ShoppingList> ShoppingLists { get; set; } = new();
    }

    /// <summary>
    /// A saved dish. Favorite marks it as a keeper so it can be filtered when
    /// building a meal plan. This used to live on the per-day entry, which meant
    /// the same dish could be simultaneously favorite and not-favorite.
    ///
    /// Categories / Directions / Equipment are stored as JSON columns (see
    /// MyContext.OnModelCreating). That keeps "array of enum" and "list of strings"
    /// without inventing join tables; filtering happens app-side, not in SQL.
    /// </summary>

    public class Recipe
    {
        public int Id { get; set; }
        public string Url { get; set; } = "";

        // The edit form already renders a validation span for Title, so the rule
        // belongs on the model too, not only on the browser.
        [Required]
        public string Title { get; set; } = "";

        // A dish can belong to several sections at once (e.g. a soup that is also
        // a main). Replaces the old single string Type.
        public List<RecipeCategory> Categories { get; set; } = new();

        public float Yield { get; set; }

        // Minutes, not TimeSpan: the forms post plain integers and every display
        // site just adds them for an "Est. Time".
        public int PrepTime { get; set; }
        public int CookTime { get; set; }

        [Range(1, 5)]
        public int Difficulty { get; set; } = 1;

        [Range(1, 5)]
        public int Rating { get; set; } = 1;

        // Kilocalories for the whole recipe (not per serving). Meal.MealCalories
        // sums these across the recipes in a meal. Non-null with a 0 default so the
        // sum is a plain integer add, with no null-handling at every call site.
        public int Calories { get; set; } = 0;

        public bool Favorite { get; set; }

        // Plain ordered strings. The old Step/Instruction table existed only to
        // carry an Order column; a List<string> is already ordered, so it goes away.
        public List<string> Directions { get; set; } = new();

        // Plain tool names. The Equipment class carried no data beyond Name (and an
        // unused IsRequired), so the class is gone.
        public List<string> Equipment { get; set; } = new();

        // "Dynamic enums": user-extensible, not a fixed C# enum. Stored as JSON like
        // the other lists. Values are normalized through TagNormalizer.Normalize on
        // save, so camping == Camping and duplicates collapse app-side. The views
        // surface all existing values as badges so one can be reused across recipes.
        public List<string> Tags { get; set; } = new();
        public List<string> Cuisine { get; set; } = new();

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // Meals this dish is cooked into — the Recipe side of the MealRecipe join.
        // Needed when deleting a recipe so its meal slots go too.
        public List<MealRecipe> MealLinks { get; set; } = new();

        public List<Ingredient> Ingredients { get; set; } = new();
    }

    public enum RecipeCategory
    {
        Appetizer,
        Breakfast,
        Soup,
        Lunch,
        Beverage,
        Salad,
        SideDish,
        MainDish,
        Dessert,
        Bread
    }

    /// <summary>
    /// A line item as written in the recipe. Never merged or converted; the
    /// shopping list groups these by (Name, Unit) rather than normalizing units.
    /// </summary>

    public class Ingredient
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public float Amount { get; set; }
        public string Unit { get; set; } = "";

        /// USDA FoodData Central identifier. Null when the ingredient was typed in
        /// free-text and never resolved against the API.
        public int? FdcId { get; set; }

        /// Per-100g kcal, cached from the lookup so nutrition totals stay offline.
        public float? CaloriesPer100g { get; set; }

        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;
    }
}
