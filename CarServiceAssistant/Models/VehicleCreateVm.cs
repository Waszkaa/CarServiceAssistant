using System.ComponentModel.DataAnnotations;
using CarServiceAssistant.Domain;
using CarServiceAssistant.ViewModels.Validation;

namespace CarServiceAssistant.ViewModels;

public class VehicleCreateVm : IValidatableObject
{
    [Required(ErrorMessage = "Podaj markę.")]
    [NotWhitespace(ErrorMessage = "Podaj markę.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "Marka musi mieć 2–60 znaków.")]
    public string Brand { get; set; } = "";

    [Required(ErrorMessage = "Podaj model.")]
    [NotWhitespace(ErrorMessage = "Podaj model.")]
    [StringLength(60, MinimumLength = 1, ErrorMessage = "Model musi mieć 1–60 znaków.")]
    public string Model { get; set; } = "";

    [Required(ErrorMessage = "Podaj rok.")]
    [Range(1950, 2100, ErrorMessage = "Rok musi być w zakresie 1950–2100.")]
    public int Year { get; set; }

    [Required(ErrorMessage = "Wybierz paliwo.")]
    public FuelType FuelType { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var maxYear = DateTime.UtcNow.Year + 1;
        if (Year > maxYear)
            yield return new ValidationResult($"Rok nie może być większy niż {maxYear}.", new[] { nameof(Year) });
    }
}
