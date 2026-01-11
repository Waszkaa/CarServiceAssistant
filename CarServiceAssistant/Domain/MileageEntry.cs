namespace CarServiceAssistant.Domain;

public class MileageEntry
{
    public int Id { get; set; }

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public int MileageKm { get; set; }
    public DateTime LoggedAtUtc { get; set; } = DateTime.UtcNow;
}
