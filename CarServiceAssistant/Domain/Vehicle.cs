namespace CarServiceAssistant.Domain;

public class Vehicle
{
    public int Id { get; set; }

    public string Brand { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int Year { get; set; }
    public string? Vin { get; set; }

    public FuelType FuelType { get; set; }

    public string UserId { get; set; } = null!;
    public AppUser User { get; set; } = null!;

    public int? CurrentMileageKm { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<MileageEntry> MileageEntries { get; set; } = new();
    public List<ServiceRecord> ServiceRecords { get; set; } = new();

}
