using System.ComponentModel.DataAnnotations;

namespace CarServiceAssistant.Domain;

public enum FuelType
{
    [Display(Name = "Benzyna")]
    Petrol = 1,

    [Display(Name = "Diesel")]
    Diesel = 2,

    [Display(Name = "Hybryda")]
    Hybrid = 3,

    [Display(Name = "Elektryczny")]
    Electric = 4,

    [Display(Name = "LPG")]
    LPG = 5
}

public enum ServiceStatus
{
    Unknown = 0,
    Ok = 1,
    Approaching = 2,
    Urgent = 3
}

public enum ServiceArea
{
    [Display(Name = "Olej silnikowy")]
    EngineOil = 1,

    [Display(Name = "Rozrząd")]
    Timing = 2,

    [Display(Name = "Hamulce")]
    Brakes = 3,

    [Display(Name = "Filtr powietrza")]
    AirFilter = 4,

    [Display(Name = "Filtr kabinowy")]
    CabinFilter = 5,

    [Display(Name = "Płyn hamulcowy")]
    BrakeFluid = 6,

    [Display(Name = "Płyn chłodniczy")]
    Coolant = 7,

    [Display(Name = "Akumulator")]
    Battery = 8,

    [Display(Name = "Inspekcja ogólna")]
    Inspection = 9
}
