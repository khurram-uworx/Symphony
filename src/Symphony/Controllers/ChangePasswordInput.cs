using System.ComponentModel.DataAnnotations;

namespace Symphony.Controllers;

public sealed class ChangePasswordInput
{
    [Required]
    [DataType(DataType.Password)]
    public string? CurrentPassword { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword))]
    public string? ConfirmPassword { get; set; }
}