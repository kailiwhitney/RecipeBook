using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RecipeBook.Models;

namespace RecipeBook.Controllers;

public class UserController : Controller
{

    private MyContext _context;

    public UserController( MyContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public IActionResult Index(){
        if(HttpContext.Session.GetInt32("uid") != null){
            return RedirectToAction("Index", "Recipe");
        }
        else{
            return View();
        }
    }

    [HttpGet("register")]
    public IActionResult Register(){
        return View();
    }

    //! Family name is what the site banner shows ("the Nguyen recipe book").
    // Falls back to the legal last name so the banner is never blank for accounts
    // that never filled the optional field.
    private static string FamilyLabel(User user) =>
        string.IsNullOrWhiteSpace(user.FamilyName) ? user.LastName : user.FamilyName;

    [HttpPost("register")]
    public IActionResult Register(User newUser){
        if(ModelState.IsValid){
            // Username is a second identity handle, so it must be unique. Checked
            // app-side (mirrors how Email uniqueness is handled) rather than via a DB
            // index, since there are no migrations checked in to add one through.
            if(_context.Users.Any(u => u.Username == newUser.Username)){
                ModelState.AddModelError("Username", "That username is already taken");
                return View(newUser);
            }
            PasswordHasher<User> hash = new PasswordHasher<User>(); // create new instance of the password hasher so that we can use it on the next line
            newUser.Password = hash.HashPassword(newUser, newUser.Password);
            // let newUser.Password = hashed version of the password
            _context.Users.Add(newUser);
            _context.SaveChanges();

            HttpContext.Session.SetInt32("uid", newUser.Id);
            HttpContext.Session.SetString("FamilyName", FamilyLabel(newUser));
            return RedirectToAction("Index", "Recipe");
        }
        return View();
    }

    [HttpPost("login")]
    public IActionResult Login(LoginUser login){
        if(ModelState.IsValid){
            User? user = _context.Users.FirstOrDefault(u => u.Email == login.Email);
            if(user == null){
                ModelState.AddModelError("LoginUser", "Invalid User");
            }
            else{
                // Was PasswordHasher<LoginUser> here while Register used
                // PasswordHasher<User>. TUser does not take part in the hash, so the
                // change is safe for hashes already stored, but two different type
                // arguments for one password was a trap waiting to spring.
                PasswordHasher<User> hash = new PasswordHasher<User>();
                var result = hash.VerifyHashedPassword(user, user.Password, login.Password);
                if(result == 0){// not a match
                    ModelState.AddModelError("LoginUser", "Invalid Password");
                }
                else{
                    HttpContext.Session.SetInt32("uid", user.Id);
                    HttpContext.Session.SetString("FamilyName", FamilyLabel(user));
                    return RedirectToAction("Index", "Recipe");
                }
            }
        }
        return View("Index");
    }

    [HttpGet("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }
}
