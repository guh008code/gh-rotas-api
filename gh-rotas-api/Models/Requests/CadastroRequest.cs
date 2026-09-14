using System.ComponentModel.DataAnnotations;

namespace gh_rotas_api.Models.Requests;

public sealed class CadastroRequest : IValidatableObject
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = "";

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Required]
    [RegularExpression(@"^\+?[0-9 ()-]{8,25}$")]
    public string Phone { get; set; } = "";

    [Required]
    [StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = "";

    [Required]
    public DateOnly? BirthDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minimumBirthDate = new DateOnly(1900, 1, 1);

        if (BirthDate.HasValue && (BirthDate.Value > today || BirthDate.Value < minimumBirthDate))
        {
            yield return new ValidationResult(
                "Data de aniversário inválida.",
                new[] { nameof(BirthDate) });
        }

        if ((Phone ?? "").Count(char.IsAsciiDigit) < 8)
        {
            yield return new ValidationResult(
                "Telefone deve conter pelo menos 8 dígitos.",
                new[] { nameof(Phone) });
        }
    }
}
