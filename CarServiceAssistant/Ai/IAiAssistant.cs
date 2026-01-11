using CarServiceAssistant.Domain;

namespace CarServiceAssistant.Ai;

public interface IAiAssistant
{
    Task<AiIntervalResult> GetTypicalIntervalsAsync(
        int vehicleId,
        string brand,
        string model,
        int year,
        FuelType fuelType,
        ServiceArea area,
        CancellationToken ct);
}
