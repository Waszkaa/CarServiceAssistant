using System.Net;
using System.Text.Json;
using CarServiceAssistant.Data;
using CarServiceAssistant.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarServiceAssistant.Ai;

public sealed class CachedAiAssistant : IAiAssistant
{
    readonly AppDbContext _db;
    readonly IAiAssistant _inner;
    readonly ILogger<CachedAiAssistant> _log;

    static readonly TimeSpan Ttl = TimeSpan.FromDays(7);

    public CachedAiAssistant(AppDbContext db, IAiAssistant inner, ILogger<CachedAiAssistant> log)
    {
        _db = db;
        _inner = inner;
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
        var now = DateTime.UtcNow;

        var cache = await _db.AiIntervalCaches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.VehicleId == vehicleId && x.Area == area, ct);

        if (cache is not null && cache.ExpiresAtUtc > now)
        {
            var fromCache = Deserialize(cache.ResultJson);
            if (fromCache is not null)
            {
                _log.LogInformation("AI cache HIT vehicleId={VehicleId} area={Area}", vehicleId, area);
                return fromCache;
            }
        }

        try
        {
            _log.LogInformation("AI cache MISS vehicleId={VehicleId} area={Area}", vehicleId, area);

            var fresh = await _inner.GetTypicalIntervalsAsync(
                vehicleId, brand, model, year, fuelType, area, ct);

            await UpsertCacheAsync(vehicleId, area, fresh, now, ct);

            return fresh;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _log.LogWarning("Gemini HTTP 429 vehicleId={VehicleId} area={Area}", vehicleId, area);

            if (cache is not null)
            {
                var stale = Deserialize(cache.ResultJson);
                if (stale is not null)
                    return stale;
            }

            return new AiIntervalResult(
                "Sugestia AI",
                new[] { "Limit zapytań do AI został chwilowo przekroczony (HTTP 429)." },
                Array.Empty<AiIntervalSource>(),
                "Spróbuj ponownie później lub skorzystaj z danych regułowych."
            );
        }
    }

    static AiIntervalResult? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<AiIntervalResult>(json); }
        catch { return null; }
    }

    async Task UpsertCacheAsync(int vehicleId, ServiceArea area, AiIntervalResult result, DateTime now, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(result);

        var existing = await _db.AiIntervalCaches
            .FirstOrDefaultAsync(x => x.VehicleId == vehicleId && x.Area == area, ct);

        if (existing is null)
        {
            _db.AiIntervalCaches.Add(new AiIntervalCache
            {
                VehicleId = vehicleId,
                Area = area,
                ResultJson = json,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(Ttl)
            });
        }
        else
        {
            existing.ResultJson = json;
            existing.CreatedAtUtc = now;
            existing.ExpiresAtUtc = now.Add(Ttl);
        }

        await _db.SaveChangesAsync(ct);
    }
}
