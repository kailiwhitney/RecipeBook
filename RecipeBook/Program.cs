using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RecipeBook.Models;
using RecipeBook.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Global auth guard: every controller action requires a live session.
// IMPORTANT: a global filter MUST be added to MvcOptions.Filters. The previous
// registration here, AddScoped<IFilterMetadata, SessionCheckFilter>, is a no-op
// that MVC never reads (filters are collected only from MvcOptions.Filters and
// [filter] attributes), so the "no session -> login page" redirect silently
// never ran. FilterCollection.Add<T>() resolves the filter from DI per request,
// which is why it must also be registered as a scoped service below.
builder.Services.AddScoped<RecipeBook.Filters.SessionCheckFilter>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<RecipeBook.Filters.SessionCheckFilter>();
});

builder.Services.AddHttpContextAccessor();

// Data-protection keys encrypt the session cookie. By default they live inside
// the container, so every `docker compose up --build` (which recreates it)
// destroys them and silently invalidates every session cookie -> everyone gets
// logged out at each rebuild. Persisting the key ring to a host volume (see
// compose.yaml) keeps logins alive across rebuilds.
var dpKeysPath = builder.Configuration["DataProtection:KeysDirectory"];
if (!string.IsNullOrWhiteSpace(dpKeysPath))
{
    Directory.CreateDirectory(dpKeysPath);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));
}

// Session lifetime: 30 days of inactivity, cookie kept for 30 days.
var sessionLifetime = TimeSpan.FromDays(30);
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "RecipeBook.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // plain HTTP on the LAN, HTTPS if proxied
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.MaxAge = sessionLifetime;
    options.IdleTimeout = sessionLifetime;
    options.Cookie.IsEssential = true; // survives "accept necessary only" consent
});
// In-process session store: fast, zero dependencies. It does NOT survive an app or
// container restart (see Session Journal note) — swap in a Redis/SQL-backed
// IDistributedCache if that ever matters.
builder.Services.AddDistributedMemoryCache(options => options.SizeLimit = 1024 * 1024);

builder.Services.AddDbContext<MyContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// Ingredient lookup is optional; the app runs with free-text names if this is empty.
builder.Services.AddHttpClient<UsdaFoodClient>();


var app = builder.Build();

// Create the database schema on startup if it doesn't exist yet.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

// UseSession must run BEFORE the endpoints execute, otherwise Session is empty
// for every action and the global auth guard bounces everyone to login.
app.UseSession();

app.UseAuthorization();


// Attribute routes on the controllers define the app's real URLs. Exactly ONE
// conventional route is registered, and only so the many
// RedirectToAction("SomeAction") calls in the controllers can still generate a
// URL. Link generation falls back to this pattern; it never shadows a real route,
// because attribute routes always beat conventional ones on precedence.
//
// There must be only one. The previous second pattern,
// "{controller=Home}/{action=Error}", also matches "/" with every value coming
// from its own defaults, so "/" ended up with three equally-ranked candidates
// (UserController.Index, RecipeController.Index, HomeController.Error) and ASP.NET
// Core answered AmbiguousMatchException -> the generic error page. The exception
// handler path /Home/Error is resolved by this same pattern, so nothing else is
// needed for it.
// Deliberately NO default values on this pattern. A pattern that supplies
// defaults for both segments also matches "/" on its own, which competes with
// the attribute route on UserController.Index (template "") for the root URL.
// Without defaults this route can only answer a two-segment path such as
// /Home/Error, so "/" belongs unambiguously to one endpoint.
app.MapControllerRoute(
    name: "conventional",
    pattern: "{controller}/{action}");

app.Run();