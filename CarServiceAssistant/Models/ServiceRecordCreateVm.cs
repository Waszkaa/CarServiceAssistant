using System.ComponentModel.DataAnnotations;
using CarServiceAssistant.Domain;

namespace CarServiceAssistant.ViewModels;

public class ServiceRecordCreateVm : IValidatableObject
{
    [Required]
    public int VehicleId { get; set; }

    [Required(ErrorMessage = "Wybierz obszar.")]
    public ServiceArea Area { get; set; }

    [Required(ErrorMessage = "Podaj datę serwisu.")]
    [DataType(DataType.Date)]
    public DateTime DoneAtDate { get; set; }

    [Required(ErrorMessage = "Podaj przebieg przy serwisie.")]
    [Range(0, 2_000_000, ErrorMessage = "Przebieg musi być w zakresie 0–2 000 000 km.")]
    public int DoneAtMileageKm { get; set; }

    [StringLength(500, ErrorMessage = "Notatka może mieć maks. 500 znaków.")]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateTime.UtcNow.Date;
        var date = DoneAtDate.Date;

        if (date > today)
            yield return new ValidationResult("Data serwisu nie może być w przyszłości.", new[] { nameof(DoneAtDate) });
    }
}
