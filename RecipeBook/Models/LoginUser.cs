#pragma warning disable CS8618
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecipeBook.Models;

[NotMapped] // don't send to database
public class LoginUser{
    [Required]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    [Display(Name ="Email")]
    public string Email {get; set;}

    [Required]
    [DataType(DataType.Password)] // auto fills input type attr
    [Display(Name = "Password")]
    public string Password { get; set; }
}
