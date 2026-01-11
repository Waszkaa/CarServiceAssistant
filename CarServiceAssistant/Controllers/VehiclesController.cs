using System.ComponentModel.DataAnnotations;
using System.Reflection;
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
public class VehiclesController : Controller
{
    readonly AppDbContext _db;
    readonly UserManager<AppUser> _userManager;
    readonly ServiceRulesEngine _rules;
    readonly IAiAssistant _ai;
    readonly ILogger<VehiclesController> _log;

    public VehiclesController(
        AppDbContext db,
        UserManager<AppUser> userManager,
        ServiceRulesEngine rules,
        IAiAssistant ai, ILogger<VehiclesController> log)
    {
        _db = db;
        _userManager = userManager;
        _rules = rules;
        _ai = ai;
        _log = log;
    }
    IQueryable<Vehicle> VehiclesScope(string? userId)
    {
        var q = _db.Vehicles.AsQueryable();

        if (!User.IsInRole("Admin"))
            q = q.Where(v => v.UserId == userId);

        return q;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);

        var vehicles = await VehiclesScope(userId)
            .OrderByDescending(v => v.CreatedAtUtc)
            .ToListAsync();

        return View(vehicles);
    }

    public IActionResult Create() => View(new VehicleCreateVm { Year = DateTime.UtcNow.Year });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleCreateVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var userId = _userManager.GetUserId(User)!;

        var v = new Vehicle
        {
            Brand = vm.Brand.Trim(),
            Model = vm.Model.Trim(),
            Year = vm.Year,
            FuelType = vm.FuelType,
            UserId = userId
        };

        _db.Vehicles.Add(v);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User);

        var v = await VehiclesScope(userId)
            .Include(x => x.MileageEntries.OrderByDescending(m => m.LoggedAtUtc))
            .FirstOrDefaultAsync(x => x.Id == id);

        if (v is null) return NotFound();
        return View(v);
    }

    public async Task<IActionResult> AddMileage(int id)
    {
        var userId = _userManager.GetUserId(User);

        var exists = await VehiclesScope(userId).AnyAsync(v => v.Id == id);
        if (!exists) return NotFound();

        ViewBag.VehicleId = id;
        return View(new MileageCreateVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMileage(int id, MileageCreateVm vm)
    {
        var userId = _userManager.GetUserId(User);

        var v = await VehiclesScope(userId).FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.VehicleId = id;
            return View(vm);
        }

        if (v.CurrentMileageKm.HasValue && vm.MileageKm < v.CurrentMileageKm.Value)
        {
            ModelState.AddModelError(nameof(vm.MileageKm),
                $"Przebieg nie może być mniejszy niż aktualny ({v.CurrentMileageKm.Value} km).");
            ViewBag.VehicleId = id;
            return View(vm);
        }

        _db.MileageEntries.Add(new MileageEntry
        {
            VehicleId = v.Id,
            MileageKm = vm.MileageKm,
            LoggedAtUtc = DateTime.UtcNow
        });

        v.CurrentMileageKm = v.CurrentMileageKm.HasValue
            ? Math.Max(v.CurrentMileageKm.Value, vm.MileageKm)
            : vm.MileageKm;

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Analysis(int id)
    {
        var userId = _userManager.GetUserId(User);

        var v = await VehiclesScope(userId)
            .Include(x => x.ServiceRecords)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (v is null) return NotFound();
        if (v.CurrentMileageKm is null)
            return RedirectToAction(nameof(AddMileage), new { id });

        var now = DateTime.UtcNow;
        var currentKm = v.CurrentMileageKm.Value;

        var items = new List<ServiceRecommendation>();

        foreach (var interval in _rules.DefaultIntervals)
        {
            var last = v.ServiceRecords
                .Where(r => r.Area == interval.Area)
                .OrderByDescending(r => r.DoneAtUtc ?? DateTime.MinValue)
                .FirstOrDefault();

            var status = _rules.Evaluate(currentKm, last?.DoneAtMileageKm, last?.DoneAtUtc, interval, now);
            items.Add(_rules.Build(interval.Area, status));
        }

        var vm = new VehicleAnalysisVm
        {
            VehicleId = v.Id,
            Title = $"{v.Brand} {v.Model} ({v.Year})",
            CurrentMileageKm = currentKm,
            Items = items
        };

        return View(vm);
    }
    [HttpGet]
    public async Task<IActionResult> AiResult(int vehicleId, int area)
    {
        if (!Enum.IsDefined(typeof(ServiceArea), area))
        {
            var defined = string.Join(", ", Enum.GetValues<ServiceArea>().Select(x => $"{(int)x}:{x}"));
            return Json(new { success = false, message = $"Niepoprawny parametr area={area}. Dozwolone: {defined}" });
        }

        var parsedArea = (ServiceArea)area;
        var userId = _userManager.GetUserId(User);

        var v = await VehiclesScope(userId)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == vehicleId);

        if (v is null)
            return Json(new { success = false, message = "Nie znaleziono pojazdu." });

        var ai = await _ai.GetTypicalIntervalsAsync(
            v.Id, v.Brand, v.Model, v.Year, v.FuelType, parsedArea, HttpContext.RequestAborted);

        return Json(new { success = true, result = ai });
    }

    static string AreaName(ServiceArea area)
    {
        var member = typeof(ServiceArea).GetMember(area.ToString()).FirstOrDefault();
        var display = member?.GetCustomAttribute<DisplayAttribute>();
        return display?.Name ?? area.ToString();
    }
}
