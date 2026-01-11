using CarServiceAssistant.Ai;
using CarServiceAssistant.Data;
using CarServiceAssistant.Domain;
using CarServiceAssistant.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=carservice.db"));

builder.Services.AddDefaultIdentity<AppUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddScoped<ServiceRulesEngine>();

var aiEnabled = builder.Configuration.GetValue<bool>("Ai:Enabled");

if (!aiEnabled)
{
    builder.Services.AddScoped<IAiAssistant, FakeAiAssistant>();
}
else
{
    builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Ai:Gemini"));

    builder.Services.AddHttpClient<GeminiAiAssistant>();

    builder.Services.AddScoped<IAiAssistant>(sp =>
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var inner = sp.GetRequiredService<GeminiAiAssistant>();
        var log = sp.GetRequiredService<ILogger<CachedAiAssistant>>();
        return new CachedAiAssistant(db, inner, log);
    });
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

    const string adminRole = "Admin";
    const string userRole = "User";

    if (!await roleManager.RoleExistsAsync(adminRole))
        await roleManager.CreateAsync(new IdentityRole(adminRole));

    if (!await roleManager.RoleExistsAsync(userRole))
        await roleManager.CreateAsync(new IdentityRole(userRole));

    var adminEmail = "lukasz.waszak@onet.eu";
    var admin = await userManager.FindByEmailAsync(adminEmail);

    if (admin is null)
    {
        admin = new AppUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var createRes = await userManager.CreateAsync(admin, "Admin123!@");
        if (createRes.Succeeded)
            await userManager.AddToRoleAsync(admin, adminRole);
    }
    else
    {
        if (!await userManager.IsInRoleAsync(admin, adminRole))
            await userManager.AddToRoleAsync(admin, adminRole);
    }
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
