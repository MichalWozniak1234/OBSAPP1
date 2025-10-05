using Microsoft.AspNetCore.Cors.Infrastructure;
using OBSAPP1.Models;
using OBSAPP1.Models;    // <- ObsOptions
using OBSAPP1.Services;  // <- ObsService

var builder = WebApplication.CreateBuilder(args);

// 1) MVC
builder.Services.AddControllersWithViews();

// 2) Rejestracja opcji z sekcji "Obs" w appsettings.json
//    "Obs": { "Path": "...\\obs64.exe", "Args": "--startstreaming" }
builder.Services.Configure<ObsOptions>(
    builder.Configuration.GetSection("Obs")); // <- bind sekcji do ObsOptions

// 3) Serwis do uruchamiania OBS (singleton – jeden na proces)
builder.Services.AddSingleton<ObsService>();

var app = builder.Build();

// --- pipeline ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");   // produkcja -> strona błędów
    app.UseHsts();                             // HSTS w prod
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
