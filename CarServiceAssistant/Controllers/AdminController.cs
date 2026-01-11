using CarServiceAssistant.Data;
using CarServiceAssistant.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarServiceAssistant.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Users()
    {
        var users = await _db.Users
            .OrderBy(u => u.Email)
            .Select(u => new AdminUserRowVm
            {
                Email = u.Email ?? "",
                VehiclesCount = _db.Vehicles.Count(v => v.UserId == u.Id)
            })
            .ToListAsync();

        return View(users);
    }
}
