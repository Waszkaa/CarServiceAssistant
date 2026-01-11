using CarServiceAssistant.Domain;

namespace CarServiceAssistant.Domain;

public class AiIntervalCache
{
    public int Id { get; set; }

    public int VehicleId { get; set; }
    public ServiceArea Area { get; set; }

    public string ResultJson { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
