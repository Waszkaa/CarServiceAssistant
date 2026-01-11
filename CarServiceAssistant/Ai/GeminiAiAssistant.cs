using System.Text;
using System.Text.Json;
using CarServiceAssistant.Domain;
using Microsoft.Extensions.Options;

namespace CarServiceAssistant.Ai;

public sealed class GeminiAiAssistant : IAiAssistant
{
    readonly HttpClient _http;
    readonly GeminiOptions _opt;
    readonly ILogger<GeminiAiAssistant> _log;

    public GeminiAiAssistant(HttpClient http, IOptions<GeminiOptions> opt, ILogger<GeminiAiAssistant> log)
    {
        _http = http;
        _opt = opt.Value;
        _log = log;
    }

    public async Task<AiIntervalResult> GetTypicalIntervalsAsync(
        int vehicleId,
        string brand,
        string model,
        int year,
        FuelType fuelType,
        ServiceArea area,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(brand, model, year, fuelType, area);

        _log.LogInformation(
            "Gemini prompt for vehicle {VehicleId} ({Brand} {Model} {Year}, {Fuel}, {Area}):\n{Prompt}",
            vehicleId,
            brand,
            model,
            year,
            fuelType,
            area,
            prompt
        );

        var modelId = NormalizeModel(_opt.Model);

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:generateContent?key={_opt.ApiKey}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            return new AiIntervalResult(
                "Sugestia AI",
                new[] { $"Błąd Gemini: HTTP {(int)res.StatusCode}" },
                Array.Empty<AiIntervalSource>(),
                "Nie udało się pobrać danych z AI. Spróbuj ponownie lub skorzystaj z danych regułowych."
            );
        }

        var parsed = TryParseAiIntervalResult(body);
        if (parsed is not null)
            return parsed;

        return new AiIntervalResult(
            "Sugestia AI",
            new[] { "Nie udało się zinterpretować odpowiedzi AI." },
            Array.Empty<AiIntervalSource>(),
            "Dane są orientacyjne. Potwierdź w instrukcji lub u mechanika."
        );
    }

    static string NormalizeModel(string model)
    {
        model = (model ?? "").Trim();
        if (model.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            model = model.Substring("models/".Length);
        return model;
    }

    static AiIntervalResult? TryParseAiIntervalResult(string rawResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);

            if (!doc.RootElement.TryGetProperty("candidates", out var candidates)) return null;
            if (candidates.GetArrayLength() == 0) return null;

            var cand = candidates[0];
            if (!cand.TryGetProperty("content", out var content)) return null;
            if (!content.TryGetProperty("parts", out var parts)) return null;
            if (parts.GetArrayLength() == 0) return null;

            var part0 = parts[0];

            string? jsonText = null;
            if (part0.TryGetProperty("text", out var text))
                jsonText = text.GetString();

            if (string.IsNullOrWhiteSpace(jsonText))
                return null;

            jsonText = jsonText.Trim();
            jsonText = jsonText.Trim('`');

            using var parsed = JsonDocument.Parse(jsonText);
            var root = parsed.RootElement;

            string GetString(string name)
                => root.TryGetProperty(name, out var p) ? (p.GetString() ?? "") : "";

            string[] GetStringArray(string name)
                => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Array
                    ? p.EnumerateArray()
                        .Select(x => StripSimpleMarkdown((x.GetString() ?? "").Trim()))
                        .Where(x => x.Length > 0)
                        .ToArray()
                    : Array.Empty<string>();

            var title = StripSimpleMarkdown(GetString("title"));
            var disclaimer = StripSimpleMarkdown(GetString("disclaimer"));

            var intervals = GetStringArray("intervals");
            var notes = GetStringArray("notes");
            var during = StripSimpleMarkdown(GetString("during"));

            var bullets = intervals
                .Select(x => x.StartsWith("•") ? x : $"• {x}")
                .Concat(notes.Select(x => x.StartsWith("•") ? x : $"• {x}"))
                .ToList();

            if (!string.IsNullOrWhiteSpace(during))
                bullets.Add(during.StartsWith("Przy okazji", StringComparison.OrdinalIgnoreCase)
                    ? during
                    : $"Przy okazji: {during}");

            if (string.IsNullOrWhiteSpace(title)) title = "Sugestia AI";
            if (string.IsNullOrWhiteSpace(disclaimer)) disclaimer = "Informacje orientacyjne.";

            return new AiIntervalResult(
                title,
                bullets.ToArray(),
                Array.Empty<AiIntervalSource>(),
                disclaimer
            );
        }
        catch
        {
            return null;
        }
    }

    static string StripSimpleMarkdown(string s)
        => (s ?? "").Replace("**", "").Replace("__", "").Trim();

    static bool IsHttpUrl(string? u)
        => Uri.TryCreate(u, UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    static string BuildPrompt(string brand, string model, int year, FuelType fuelType, ServiceArea area)
    {
        var spec = AreaSpec(area);

        return
            "Zwróć WYŁĄCZNIE poprawny JSON w formacie:\n" +
            "{\"title\":\"...\",\"intervals\":[\"...\"],\"notes\":[\"...\"],\"during\":\"Przy okazji ...\",\"disclaimer\":\"...\"}\n" +
            "Bez markdown, bez dodatkowego tekstu.\n" +
            $"Auto: {brand} {model} ({year}), paliwo: {fuelType}.\n" +
            $"Temat: {spec.Title}.\n" +
            "Wymagania dla odpowiedzi:\n" +
            $"- {spec.Instructions}\n" +
            "- intervals: 2–3 punkty tylko o interwałach (km + czas).\n" +
            "- notes: 2–3 punkty (objawy/czynniki skracające/uwagi zależnie od tematu).\n" +
            "- during: dokładnie 1 zdanie zaczynające się od \"Przy okazji\" i zawierające 2–4 elementy do sprawdzenia.\n" +
            "- Każdy element w intervals i notes ma być CZYSTYM TEKSTEM, bez wypunktowań, bez znaków '•', '-', '*', ani numeracji.\n" +
            "- Nie dodawaj porad ogólnych niezwiązanych z tematem.\n" +
            "- Disclaimer: 1 zdanie (informacyjnie, bez straszenia).\n";
    }

    static string AreaTopicPl(ServiceArea area) => area switch
    {
        ServiceArea.EngineOil => "wymiana oleju silnikowego (z filtrem oleju)",
        ServiceArea.Brakes => "hamulce: kontrola i typowe interwały wymiany klocków oraz tarcz",
        ServiceArea.BrakeFluid => "płyn hamulcowy: kontrola i typowy interwał wymiany",
        ServiceArea.Timing => "rozrząd (łańcuch/pasek): kontrola, objawy i typowe przebiegi",
        ServiceArea.AirFilter => "filtr powietrza: typowy interwał wymiany",
        ServiceArea.CabinFilter => "filtr kabinowy: typowy interwał wymiany",
        ServiceArea.Coolant => "płyn chłodniczy: typowy interwał wymiany i kontrola",
        ServiceArea.Battery => "akumulator: typowa żywotność i kontrola",
        ServiceArea.Inspection => "inspekcja ogólna: co sprawdzić okresowo",
        _ => area.ToString()
    };

    static (string Title, string Instructions) AreaSpec(ServiceArea area) => area switch
    {
        ServiceArea.EngineOil => (
            "Olej silnikowy",
            "Podaj typowe interwały wymiany oleju silnikowego i filtra oleju: zakres w km ORAZ czas (miesiące/lata). " +
            "Dodaj 1 punkt: czynniki skracające interwał (miasto/krótkie trasy/DPF). " +
            "Dodaj 1 punkt zaczynający się od 'Przy okazji wymiany:' – na co zwrócić uwagę (np. wycieki, stan korka/spustu, filtr, poziom/kolor oleju, odma/odpowietrzenie jeśli dotyczy). " +
            "Nie pisz o oponach ani o przeglądach ogólnych."
        ),

        ServiceArea.Brakes => (
            "Hamulce",
            "Podaj typowe zakresy dla: klocki przód, klocki tył, tarcze przód, tarcze tył (km albo przedziały zużycia). " +
            "Dodaj 1 punkt: objawy zużycia hamulców. " +
            "Dodaj 1 punkt: czynniki przyspieszające zużycie (miasto, styl jazdy, masa auta). " +
            "Dodaj 1 punkt zaczynający się od 'Przy okazji wymiany:' – na co zwrócić uwagę (np. prowadnice zacisku, stan osłon, czujniki zużycia, bicie tarcz, płyn/wycieki). " +
            "Nie pisz o oleju silnikowym ani przeglądach ogólnych."
        ),

        ServiceArea.BrakeFluid => (
            "Płyn hamulcowy",
            "Podaj typowy interwał wymiany płynu hamulcowego (czas, np. co 2 lata) i krótko dlaczego. " +
            "Dodaj 1 punkt zaczynający się od 'Przy okazji wymiany:' – na co zwrócić uwagę (np. odpowietrzenie, stan przewodów, wycieki, kolor płynu, ABS/ESP jeśli wymaga procedury). " +
            "Nie pisz o klockach/tarczach ani oleju silnikowym."
        ),

        ServiceArea.AirFilter => (
            "Filtr powietrza",
            "Podaj typowy interwał wymiany filtra powietrza (km i/lub czas) oraz kiedy skracać (kurz/miasto). " +
            "Dodaj 1 punkt zaczynający się od 'Przy okazji wymiany:' – na co zwrócić uwagę (np. uszczelka obudowy, zabrudzenia w dolocie, poprawne domknięcie obudowy, ewentualne pęknięcia). " +
            "Nie pisz o oleju ani przeglądach ogólnych."
        ),

        ServiceArea.CabinFilter => (
            "Filtr kabinowy",
            "Podaj typowy interwał wymiany filtra kabinowego (km i/lub czas) oraz objawy zużycia. " +
            "Dodaj 1 punkt zaczynający się od 'Przy okazji wymiany:' – na co zwrócić uwagę (np. kierunek montażu, drożność odpływów podszybia, zapach/wilgoć, parowanie). " +
            "Nie pisz o oleju ani przeglądach ogólnych."
        ),

        ServiceArea.Coolant => (
            "Płyn chłodniczy",
            "Podaj typowy interwał wymiany płynu chłodniczego (lata) oraz kiedy wymienić wcześniej (naprawy/nieznany płyn). " +
            "Dodaj 1 punkt zaczynający się od 'Przy okazji wymiany:' – na co zwrócić uwagę (np. szczelność układu, stan węży i opasek, korek zbiorniczka, odpowietrzenie, kolor/typ płynu). " +
            "Nie pisz o oleju ani przeglądach ogólnych."
        ),

        ServiceArea.Battery => (
            "Akumulator",
            "Podaj typową żywotność akumulatora (lata) i 2–3 symptomy zużycia oraz czynniki skracające. " +
            "Dodaj 1 punkt zaczynający się od 'Przy okazji wymiany:' – na co zwrócić uwagę (np. kodowanie/rejestracja w autach które tego wymagają, klemy i masa, alternator/ładowanie, zabezpieczenie pamięci). " +
            "Nie pisz o oleju ani przeglądach ogólnych."
        ),

        ServiceArea.Timing => (
            "Rozrząd",
            "Podaj ogólne typowe interwały: pasek (km + lata) i łańcuch (typowo bez stałego interwału + objawy). " +
            "Dodaj 1 punkt zaczynający się od 'Przy okazji wymiany:' – na co zwrócić uwagę (np. pompa wody/rolki/uszczelniacze przy pasku; przy łańcuchu napinacz/ślizgi; wycieki z okolic pokryw). " +
            "Nie pisz o oleju silnikowym ani przeglądach ogólnych."
        ),

        ServiceArea.Inspection => (
            "Inspekcja ogólna",
            "Podaj typowy interwał inspekcji ogólnej (czas/km) i przykładowe elementy do sprawdzenia (max 4). " +
            "Dodaj 1 punkt zaczynający się od 'Przy okazji inspekcji:' – na co zwrócić uwagę (np. wycieki, luzy, stan osłon, korozja). " +
            "Nie rób listy 'przed jazdą'."
        ),

        _ => (area.ToString(),
            "Podaj typowe interwały i krótkie wskazówki tylko dla tego obszaru. " +
            "Dodaj 1 punkt zaczynający się od 'Przy okazji wymiany:' – na co zwrócić uwagę.")
    };


}
