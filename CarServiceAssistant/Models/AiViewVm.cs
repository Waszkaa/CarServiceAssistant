using CarServiceAssistant.Domain;

namespace CarServiceAssistant.ViewModels;

public class AiViewVm
{
    public int VehicleId { get; set; }
    public ServiceArea Area { get; set; }

    public string VehicleTitle { get; set; } = "";

    public string RulesSummary { get; set; } = "";
    public List<string> RulesIntervals { get; set; } = new();
    public List<string> RulesDetails { get; set; } = new();

}
