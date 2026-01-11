using CarServiceAssistant.Services;

namespace CarServiceAssistant.ViewModels;

public class ServiceListWithAiVm
{
    public int VehicleId { get; set; }
    public List<ServiceRecommendation> Items { get; set; } = new();
}
