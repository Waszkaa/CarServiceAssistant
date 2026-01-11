using CarServiceAssistant.Domain;

namespace CarServiceAssistant.Ai;

public class FakeAiAssistant : IAiAssistant
{
    public Task<AiIntervalResult> GetTypicalIntervalsAsync(
        int vehicleId,
        string brand,
        string model,
        int year,
        FuelType fuelType,
        ServiceArea area,
        CancellationToken ct)
    {
        var nowYear = DateTime.UtcNow.Year;
        var vehicleAge = Math.Max(0, nowYear - year);

        var title = $"Szacunkowe interwały serwisowe: {brand} {model} ({year}), {fuelType}, obszar: {area}";

        var bullets = BuildBullets(area, fuelType, vehicleAge).ToArray();

        var sources = new[]
        {
            new AiIntervalSource("Dane regułowe (fallback)", "https://example.com/fallback-rules"),
            new AiIntervalSource("Praktyka serwisowa – wartości orientacyjne", "https://example.com/service-practice")
        };

        var disclaimer =
            "Wartości są orientacyjne i mają charakter informacyjny. " +
            "Dokładny interwał zależy od stylu jazdy, warunków eksploatacji i historii serwisowej. " +
            "Zaleca się weryfikację w instrukcji producenta lub u mechanika.";

        return Task.FromResult(new AiIntervalResult(title, bullets, sources, disclaimer));
    }

    static IEnumerable<string> BuildBullets(ServiceArea area, FuelType fuelType, int vehicleAge)
    {
        switch (area)
        {
            case ServiceArea.EngineOil:
                foreach (var s in OilBullets(fuelType, vehicleAge)) yield return s;
                yield break;

            case ServiceArea.AirFilter:
                yield return "Filtr powietrza: zwykle co 20–30 tys. km lub co 12–24 miesiące.";
                yield return "Częsta jazda miejska lub zapylone środowisko skraca interwał.";
                yield break;

            case ServiceArea.CabinFilter:
                yield return "Filtr kabinowy: zwykle co 10–15 tys. km lub raz w roku.";
                yield return "Objawy zużycia: parowanie szyb, słaby nawiew, zapachy.";
                yield break;

            case ServiceArea.BrakeFluid:
                yield return "Płyn hamulcowy: najczęściej co 24 miesiące, niezależnie od przebiegu.";
                yield return "Wchłanianie wilgoci obniża skuteczność hamowania.";
                yield break;

            case ServiceArea.Coolant:
                yield return "Płyn chłodniczy: zwykle co 4–5 lat (zależnie od specyfikacji).";
                yield return "Po naprawach układu chłodzenia warto rozważyć wcześniejszą wymianę.";
                yield break;

            case ServiceArea.Battery:
                yield return "Akumulator: typowa żywotność 4–6 lat.";
                yield return "Krótkie trasy i niskie temperatury skracają żywotność.";
                yield break;

            case ServiceArea.Brakes:
                yield return "Hamulce: brak stałego interwału km – zużycie zależne od stylu jazdy.";
                yield return "Kontrola klocków i tarcz co 10–15 tys. km lub sezonowo.";
                yield return "Miasto przyspiesza zużycie klocków, rzadkie jazdy sprzyjają korozji tarcz.";
                yield break;

            case ServiceArea.Timing:
                yield return "Rozrząd: interwał zależny od typu (pasek/łańcuch) i silnika.";
                yield return "Pasek: zwykle 90–180 tys. km lub 5–10 lat. Łańcuch: kontrola objawów zużycia.";
                yield break;

            case ServiceArea.Inspection:
                yield return "Inspekcja ogólna: zwykle co 12 miesięcy lub zgodnie z przepisami.";
                yield return "Kontrola obejmuje płyny, hamulce, zawieszenie, opony i oświetlenie.";
                yield break;

            default:
                yield return "Brak danych dla tego obszaru.";
                yield break;
        }
    }

    static IEnumerable<string> OilBullets(FuelType fuelType, int vehicleAge)
    {
        if (fuelType == FuelType.Electric)
        {
            yield return "Olej silnikowy: nie dotyczy pojazdów elektrycznych.";
            yield break;
        }

        yield return "Olej silnikowy: zwykle co 10–15 tys. km lub co 12 miesięcy.";
        yield return "Krótkie trasy i jazda miejska sprzyjają skracaniu interwałów.";

        if (vehicleAge >= 12)
            yield return "Starsze pojazdy: częstsza kontrola poziomu i zużycia oleju jest wskazana.";

        if (fuelType == FuelType.Diesel)
            yield return "Diesel: częste jazdy miejskie mogą prowadzić do rozrzedzania oleju paliwem.";
    }
}
