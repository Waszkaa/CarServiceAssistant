using CarServiceAssistant.Ai;
using CarServiceAssistant.Data;
using CarServiceAssistant.Domain;
using CarServiceAssistant.Services;
using CarServiceAssistant.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarServiceAssistant.Controllers;

[Authorize]
public class ServiceRecordsController : Controller
{
    readonly AppDbContext _db;
    readonly UserManager<AppUser> _userManager;
    readonly IAiAssistant _ai;
    readonly ServiceRulesEngine _rules;

    public ServiceRecordsController(AppDbContext db, UserManager<AppUser> userManager, IAiAssistant ai, ServiceRulesEngine rules)
    {
        _db = db;
        _userManager = userManager;
        _ai = ai;
        _rules = rules;
    }

    public async Task<IActionResult> Index(int vehicleId)
    {
        var userId = _userManager.GetUserId(User)!;

        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.UserId == userId);

        if (vehicle is null) return NotFound();

        var items = await _db.ServiceRecords
            .AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.DoneAtUtc ?? DateTime.MinValue)
            .ThenByDescending(r => r.DoneAtMileageKm ?? -1)
            .ToListAsync();

        ViewBag.VehicleTitle = $"{vehicle.Brand} {vehicle.Model} ({vehicle.Year})";
        ViewBag.VehicleId = vehicleId;

        return View(items);
    }

    public async Task<IActionResult> Create(int vehicleId)
    {
        var userId = _userManager.GetUserId(User)!;

        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.UserId == userId);

        if (vehicle is null) return NotFound();

        ViewBag.VehicleTitle = $"{vehicle.Brand} {vehicle.Model} ({vehicle.Year})";
        ViewBag.CurrentMileage = vehicle.CurrentMileageKm;

        return View(new ServiceRecordCreateVm
        {
            VehicleId = vehicleId,
            DoneAtDate = DateTime.Today,
            DoneAtMileageKm = vehicle.CurrentMileageKm ?? 0
        });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceRecordCreateVm vm)
    {
        var userId = _userManager.GetUserId(User)!;

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vm.VehicleId && v.UserId == userId);

        if (vehicle is null) return NotFound();

        if (vm.DoneAtDate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(vm.DoneAtDate), "Data serwisu nie może być w przyszłości.");

        if (vehicle.CurrentMileageKm.HasValue && vm.DoneAtMileageKm > vehicle.CurrentMileageKm.Value)
            ModelState.AddModelError(nameof(vm.DoneAtMileageKm), "Przebieg serwisu nie może być większy niż aktualny przebieg pojazdu.");

        if (!ModelState.IsValid)
        {
            ViewBag.VehicleTitle = $"{vehicle.Brand} {vehicle.Model} ({vehicle.Year})";
            ViewBag.CurrentMileage = vehicle.CurrentMileageKm;
            return View(vm);
        }

        var record = new ServiceRecord
        {
            VehicleId = vm.VehicleId,
            Area = vm.Area,
            DoneAtUtc = vm.DoneAtDate.Date.ToUniversalTime(),
            DoneAtMileageKm = vm.DoneAtMileageKm,
            Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim()
        };

        _db.ServiceRecords.Add(record);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { vehicleId = vm.VehicleId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User)!;

        var record = await _db.ServiceRecords
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id && r.Vehicle.UserId == userId);

        if (record is null) return NotFound();

        var vehicleId = record.VehicleId;

        _db.ServiceRecords.Remove(record);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index), new { vehicleId });
    }

    [HttpGet]
    public async Task<IActionResult> Ai(int vehicleId, ServiceArea area, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User)!;

        var v = await _db.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == vehicleId && x.UserId == userId, ct);

        if (v is null) return NotFound();

        var interval = _rules.DefaultIntervals.FirstOrDefault(x => x.Area == area);

        string rulesSummary;
        var rulesIntervals = new List<string>();
        var rulesDetails = new List<string>();

        if (interval is null)
        {
            rulesSummary = "Brak reguł systemowych dla tego obszaru.";
        }
        else
        {
            rulesSummary = "Zalecane interwały serwisowe (reguły systemowe)";

            if (interval.EveryKm is not null && interval.EveryMonths is not null)
            {
                rulesIntervals.Add($"Co {interval.EveryKm.Value:N0} km lub co {interval.EveryMonths.Value} miesięcy (w zależności co nastąpi wcześniej).");
            }
            else if (interval.EveryKm is not null)
            {
                rulesIntervals.Add($"Co {interval.EveryKm.Value:N0} km.");
            }
            else if (interval.EveryMonths is not null)
            {
                rulesIntervals.Add($"Co {interval.EveryMonths.Value} miesięcy.");
            }
            else
            {
                rulesIntervals.Add("Brak stałego interwału — zalecana kontrola objawów zużycia i inspekcja.");
            }

            if (interval.EveryKm is not null && interval.ApproachingKmBefore is not null)
                rulesDetails.Add($"Status „zbliża się” pojawia się ok. {interval.ApproachingKmBefore.Value:N0} km przed terminem.");

            if (interval.EveryMonths is not null && interval.ApproachingMonthsBefore is not null)
                rulesDetails.Add($"Status „zbliża się” pojawia się ok. {interval.ApproachingMonthsBefore.Value} mies. przed terminem.");

            rulesDetails.Add("Te wartości są orientacyjne i pomagają planować serwis przy braku danych producenta / książki serwisowej.");
        }

        var vm = new AiViewVm
        {
            VehicleId = v.Id,
            Area = area,
            VehicleTitle = $"{v.Brand} {v.Model} ({v.Year})",
            RulesSummary = rulesSummary,
            RulesIntervals = rulesIntervals,
            RulesDetails = rulesDetails
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> AiResult(int vehicleId, ServiceArea area, CancellationToken ct)
    {
        var userId = _userManager.GetUserId(User)!;

        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == vehicleId && x.UserId == userId, ct);

        if (vehicle is null)
            return Json(new { success = false, message = "Nie znaleziono pojazdu." });

        var res = await _ai.GetTypicalIntervalsAsync(
            vehicleId,
            vehicle.Brand,
            vehicle.Model,
            vehicle.Year,
            vehicle.FuelType,
            area,
            ct);

        return Json(new
        {
            success = true,
            result = new
            {
                plainLanguageSummary = res.PlainLanguageSummary,
                keyIntervals = res.KeyIntervals,
                sources = res.Sources.Select(s => new { title = s.Title, url = s.Url }),
                safetyNote = res.SafetyNote
            }
        });
    }



}
