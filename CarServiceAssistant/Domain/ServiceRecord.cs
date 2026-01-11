namespace CarServiceAssistant.Domain;

public class ServiceRecord
{
    public int Id { get; set; }

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public ServiceArea Area { get; set; }

    public int? DoneAtMileageKm { get; set; }
    public DateTime? DoneAtUtc { get; set; }

    public string? Notes { get; set; }
}
