using CarServiceAssistant.Domain;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace CarServiceAssistant.Services;

public class ServiceRulesEngine
{
    public IReadOnlyList<ServiceInterval> DefaultIntervals { get; } = new List<ServiceInterval>
    {
        new(ServiceArea.EngineOil, 15000, 2000, 12, 2),
        new(ServiceArea.AirFilter, 30000, 3000, 24, 3),
        new(ServiceArea.CabinFilter, 15000, 2000, 12, 2),
        new(ServiceArea.BrakeFluid, null, null, 24, 3),
        new(ServiceArea.Coolant, null, null, 60, 6),
        new(ServiceArea.Battery, null, null, 48, 6),
        new(ServiceArea.Brakes, null, null, null, null),
        new(ServiceArea.Timing, null, null, null, null),
        new(ServiceArea.Inspection, 15000, 2000, 12, 2),
    };

    public ServiceStatus Evaluate(int currentMileageKm, int? lastMileageKm, DateTime? lastDateUtc, ServiceInterval interval, DateTime nowUtc)
    {
        if (interval.EveryKm is null && interval.EveryMonths is null)
            return ServiceStatus.Unknown;

        var statusKm = EvaluateKm(currentMileageKm, lastMileageKm, interval);
        var statusTime = EvaluateTime(lastDateUtc, interval, nowUtc);

        return Combine(statusKm, statusTime);
    }

    static ServiceStatus Combine(ServiceStatus km, ServiceStatus time)
    {
        if (km == ServiceStatus.Unknown || time == ServiceStatus.Unknown)
            return ServiceStatus.Unknown;

        return (ServiceStatus)Math.Max((int)km, (int)time);
    }

    static ServiceStatus EvaluateKm(int currentMileageKm, int? lastMileageKm, ServiceInterval interval)
    {
        if (interval.EveryKm is null) return ServiceStatus.Ok;
        if (lastMileageKm is null) return ServiceStatus.Unknown;

        var dueAt = lastMileageKm.Value + interval.EveryKm.Value;
        if (currentMileageKm >= dueAt) return ServiceStatus.Urgent;

        var approachingAt = dueAt - (interval.ApproachingKmBefore ?? 0);
        if (currentMileageKm >= approachingAt) return ServiceStatus.Approaching;

        return ServiceStatus.Ok;
    }

    static ServiceStatus EvaluateTime(DateTime? lastDateUtc, ServiceInterval interval, DateTime nowUtc)
    {
        if (interval.EveryMonths is null) return ServiceStatus.Ok;
        if (lastDateUtc is null) return ServiceStatus.Unknown;

        var dueAt = lastDateUtc.Value.AddMonths(interval.EveryMonths.Value);
        if (nowUtc >= dueAt) return ServiceStatus.Urgent;

        var approachingAt = dueAt.AddMonths(-(interval.ApproachingMonthsBefore ?? 0));
        if (nowUtc >= approachingAt) return ServiceStatus.Approaching;

        return ServiceStatus.Ok;
    }

    public ServiceRecommendation Build(ServiceArea area, ServiceStatus status)
    {
        return area switch
        {
            ServiceArea.EngineOil => new(area, status,
                "Olej silnikowy",
                status switch
                {
                    ServiceStatus.Unknown => "Brak danych o wymianie oleju — zalecana weryfikacja lub wymiana, jeśli nie masz pewności.",
                    ServiceStatus.Ok => "Wygląda, że olej jest w bezpiecznym zakresie interwału.",
                    ServiceStatus.Approaching => "Zbliża się termin wymiany oleju — warto zaplanować serwis.",
                    _ => "Wymiana oleju jest pilna — odkładanie może przyspieszać zużycie silnika."
                },
                "Uzupełnij ostatni przebieg i/lub datę wymiany oleju, jeśli je znasz.",
                "Jeśli nie pamiętasz, przy najbliższym serwisie wymień olej + filtr i zapisz wpis w historii."
            ),

            ServiceArea.BrakeFluid => new(area, status,
                "Płyn hamulcowy",
                status switch
                {
                    ServiceStatus.Unknown => "Brak danych o wymianie płynu hamulcowego — zalecana kontrola lub wymiana, jeśli nie masz pewności.",
                    ServiceStatus.Ok => "Płyn hamulcowy prawdopodobnie jest jeszcze w normie.",
                    ServiceStatus.Approaching => "Zbliża się czas wymiany płynu hamulcowego.",
                    _ => "Wymiana płynu hamulcowego jest pilna — wpływa na skuteczność hamowania."
                },
                "Jeśli pamiętasz, wpisz datę ostatniej wymiany płynu.",
                "Poproś o pomiar zawartości wody w płynie testerem."
            ),

            _ => new(area, status,
                AreaName(area),
                status switch
                {
                    ServiceStatus.Unknown => "Brak danych serwisowych — nie da się ocenić stanu. Zalecana weryfikacja lub wymiana.",
                    ServiceStatus.Ok => "Na ten moment wygląda, że jest OK.",
                    ServiceStatus.Approaching => "Zbliża się termin — warto to zaplanować.",
                    _ => "To wygląda na pilne — nie odkładaj zbyt długo."
                },
                "Uzupełnij historię serwisową, jeśli masz te informacje.",
                "Jeśli nie masz pewności, sprawdź przy najbliższej wizycie."
            )
        };
    }

    static string AreaName(ServiceArea area)
    {
        var member = typeof(ServiceArea)
            .GetMember(area.ToString())
            .FirstOrDefault();

        var display = member?
            .GetCustomAttribute<DisplayAttribute>();

        return display?.Name ?? area.ToString();
    }
}
