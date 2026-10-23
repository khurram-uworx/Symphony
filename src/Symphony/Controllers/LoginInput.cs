using System.ComponentModel.DataAnnotations;

namespace Symphony.Controllers;

public sealed class LoginInput
{
    [Required]
    public string? UserName { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string? Password { get; set; }
}
