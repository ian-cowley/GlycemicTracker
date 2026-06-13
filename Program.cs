using CarbTracker.Data;
using CarbTracker.Services;
using System;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// Configure Windows Service hosting
builder.Services.AddWindowsService();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register CarbTracker dependencies for raw ADO.NET and calculations
builder.Services.AddSingleton<DatabaseHelper>();
builder.Services.AddSingleton<FoodRepository>();
builder.Services.AddSingleton<LogRepository>();
builder.Services.AddSingleton<GlucoseCalculator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Automatically initialize and seed the SQL Server LocalDB database on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbHelper = scope.ServiceProvider.GetRequiredService<DatabaseHelper>();
        await DbInitializer.InitializeAsync(dbHelper.ConnectionString);
        Console.WriteLine("Database initialized and seeded successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred while initializing the database: {ex.Message}");
    }
}

app.Run();
