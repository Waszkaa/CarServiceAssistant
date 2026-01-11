using CarServiceAssistant.Domain;
using CarServiceAssistant.Services;

namespace CarServiceAssistant.ViewModels;

public class VehicleAnalysisVm
{
    public int VehicleId { get; set; }
    public string Title { get; set; } = null!;
    public int CurrentMileageKm { get; set; }

    public List<ServiceRecommendation> Items { get; set; } = new();

    public List<ServiceRecommendation> DoNow => Items.Where(i => i.Status == ServiceStatus.Urgent).ToList();
    public List<ServiceRecommendation> CheckSoon => Items.Where(i => i.Status == ServiceStatus.Approaching).ToList();
}
