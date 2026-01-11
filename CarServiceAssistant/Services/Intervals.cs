using CarServiceAssistant.Domain;

namespace CarServiceAssistant.Services;

public record ServiceInterval(
    ServiceArea Area,
    int? EveryKm,
    int? ApproachingKmBefore,
    int? EveryMonths,
    int? ApproachingMonthsBefore
);
