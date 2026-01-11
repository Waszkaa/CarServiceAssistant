using System.ComponentModel.DataAnnotations;

namespace CarServiceAssistant.ViewModels;

public class MileageCreateVm
{
    [Required(ErrorMessage = "Podaj przebieg.")]
    [Range(0, 2_000_000, ErrorMessage = "Przebieg musi być w zakresie 0–2 000 000 km.")]
    public int MileageKm { get; set; }
}
