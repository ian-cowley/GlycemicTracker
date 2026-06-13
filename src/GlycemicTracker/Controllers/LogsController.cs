using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GlycemicTracker.Models;
using GlycemicTracker.Data;
using GlycemicTracker.Services;

namespace GlycemicTracker.Controllers
{
    public class LogsController : Controller
    {
        private readonly LogRepository _logRepo;
        private readonly FoodRepository _foodRepo;

        public LogsController(LogRepository logRepo, FoodRepository foodRepo)
        {
            _logRepo = logRepo;
            _foodRepo = foodRepo;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFoodLog(int foodId, double amountGrams, string logTimeStr, double? timeOffsetMinutes)
        {
            var food = await _foodRepo.GetFoodByIdAsync(foodId);
            if (food == null)
            {
                TempData["ErrorMessage"] = "Selected food item not found.";
                return RedirectToAction("Index", "Home");
            }

            if (amountGrams <= 0)
            {
                TempData["ErrorMessage"] = "Portion size must be greater than 0 grams.";
                return RedirectToAction("Index", "Home");
            }

            // Determine log time (handling custom backdate or time offset)
            DateTime logTime = TimeHelper.UkNow;
            if (timeOffsetMinutes.HasValue)
            {
                logTime = logTime.AddMinutes(-timeOffsetMinutes.Value);
            }
            else if (!string.IsNullOrWhiteSpace(logTimeStr))
            {
                if (DateTime.TryParse(logTimeStr, out var parsedTime))
                {
                    logTime = parsedTime;
                }
            }

            // Calculate exact nutrient macros consumed
            double scale = amountGrams / 100.0;
            var carbs = food.CarbsPer100g * scale;
            var sugars = food.SugarPer100g * scale;
            var fiber = food.FiberPer100g * scale;
            var protein = food.ProteinPer100g * scale;
            var fat = food.FatPer100g * scale;
            var alcohol = food.AlcoholGrams * scale;

            // Calculate glycemic load
            double glycemicLoad = (carbs * food.GlycemicIndex) / 100.0;

            var newLog = new FoodLog
            {
                FoodId = food.Id,
                AmountGrams = amountGrams,
                LogTime = logTime,
                CarbsGrams = Math.Round(carbs, 2),
                SugarGrams = Math.Round(sugars, 2),
                FiberGrams = Math.Round(fiber, 2),
                ProteinGrams = Math.Round(protein, 2),
                FatGrams = Math.Round(fat, 2),
                GlycemicLoad = Math.Round(glycemicLoad, 2),
                AlcoholGrams = Math.Round(alcohol, 2)
            };

            await _logRepo.AddFoodLogAsync(newLog);
            TempData["SuccessMessage"] = $"Logged {amountGrams}g of {food.Name} successfully.";

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddGlucoseReading(double glucoseValue, string readingTimeStr)
        {
            if (glucoseValue < 1.0 || glucoseValue > 35.0)
            {
                TempData["ErrorMessage"] = "Glucose value must be a realistic number between 1.0 and 35.0 mmol/L.";
                return RedirectToAction("Index", "Home");
            }

            DateTime readingTime = TimeHelper.UkNow;
            if (!string.IsNullOrWhiteSpace(readingTimeStr))
            {
                if (DateTime.TryParse(readingTimeStr, out var parsedTime))
                {
                    readingTime = parsedTime;
                }
            }

            var reading = new GlucoseReading
            {
                ReadingTime = readingTime,
                GlucoseValue = Math.Round(glucoseValue, 1),
                Notes = "Manual finger prick reading"
            };

            await _logRepo.AddGlucoseReadingAsync(reading);
            TempData["SuccessMessage"] = $"Logged glucose reading of {glucoseValue} mmol/L.";

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFoodLog(int id)
        {
            await _logRepo.DeleteFoodLogAsync(id);
            TempData["SuccessMessage"] = "Food log deleted.";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGlucoseReading(int id)
        {
            await _logRepo.DeleteGlucoseReadingAsync(id);
            TempData["SuccessMessage"] = "Glucose reading deleted.";
            return RedirectToAction("Index", "Home");
        }
    }
}
