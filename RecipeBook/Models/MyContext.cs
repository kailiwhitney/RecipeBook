using Microsoft.EntityFrameworkCore;

namespace RecipeBook.Models;

public class MyContext : DbContext
{   
    public MyContext(DbContextOptions options) : base(options) { }   

    public DbSet<User> Users { get; set; }
    public DbSet<Ingredient> Ingredients { get; set; }
    public DbSet<Recipe> Recipes { get; set; }
    public DbSet<MealPlan> MealPlans { get; set; }
    public DbSet<Meal> Meals { get; set; }
    public DbSet<MealRecipe> MealRecipes { get; set; }
    public DbSet<ShoppingList> ShoppingLists { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Categories / Directions / Equipment / Tags / Cuisine are lists on the
        // model but single text columns in MySQL. They serialize through a value
        // converter (see JsonList). NOTE: Property(...).ToJson() is EF Core 9; on
        // this EF8/Pomelo8 stack it does not exist, which is the CS1929 these used
        // to raise. Queries over these lists still run app-side, not in SQL.
        var recipe = modelBuilder.Entity<Recipe>();
        recipe.Property(r => r.Categories).HasConversion(
            (List<RecipeCategory> v) => JsonList.Write(v),
            (string? v) => JsonList.Read<RecipeCategory>(v)).HasMaxLength(200);
        recipe.Property(r => r.Directions).HasConversion(
            (List<string> v) => JsonList.Write(v),
            (string? v) => JsonList.Read<string>(v)).HasMaxLength(8000);
        recipe.Property(r => r.Equipment).HasConversion(
            (List<string> v) => JsonList.Write(v),
            (string? v) => JsonList.Read<string>(v)).HasMaxLength(4000);
        recipe.Property(r => r.Tags).HasConversion(
            (List<string> v) => JsonList.Write(v),
            (string? v) => JsonList.Read<string>(v)).HasMaxLength(2000);
        recipe.Property(r => r.Cuisine).HasConversion(
            (List<string> v) => JsonList.Write(v),
            (string? v) => JsonList.Read<string>(v)).HasMaxLength(2000);

        // A day should not hold two meals of the same type. Replaces the old
        // (plan, date, position) slot constraint from PlanEntry.
        modelBuilder.Entity<Meal>()
            .HasIndex(m => new { m.MealPlanId, m.Date, m.Type })
            .IsUnique();

        // Explicit Meal <-> Recipe join. The pair is unique so a recipe can be
        // added to a given meal only once; the surrogate Id lets a single pairing
        // be removed without disturbing the others.
        modelBuilder.Entity<MealRecipe>()
            .HasIndex(j => new { j.MealId, j.RecipeId })
            .IsUnique();

        modelBuilder.Entity<Ingredient>()
            .Property(i => i.FdcId)
            .IsRequired(false);
    }
}
