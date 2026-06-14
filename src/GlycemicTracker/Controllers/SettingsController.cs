using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GlycemicTracker.Data;
using GlycemicTracker.Models;

namespace GlycemicTracker.Controllers
{
    public class SettingsController : Controller
    {
        private readonly SettingsRepository _settingsRepository;

        public SettingsController(SettingsRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
        }

        // GET: Settings
        public async Task<IActionResult> Index()
        {
            var parameters = await _settingsRepository.GetAllParametersAsync();
            return View(parameters);
        }

        // POST: Settings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GlycemicParameters param)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _settingsRepository.AddParameterAsync(param);
                    TempData["SuccessMessage"] = $"Sensitivity adjustments for {param.StartDate:dd MMM yyyy} saved successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Failed to save adjustments. A record for this date may already exist. Error: {ex.Message}");
                }
            }

            var parameters = await _settingsRepository.GetAllParametersAsync();
            return View("Index", parameters);
        }

        // POST: Settings/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _settingsRepository.DeleteParameterAsync(id);
                TempData["SuccessMessage"] = "Sensitivity settings deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
