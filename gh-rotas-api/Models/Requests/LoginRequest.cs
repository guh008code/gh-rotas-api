using System.ComponentModel.DataAnnotations;

namespace gh_rotas_api.Models.Requests;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = "";
}
