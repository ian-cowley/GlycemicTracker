using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GlycemicTracker.Models;
using GlycemicTracker.Data;

namespace GlycemicTracker.Controllers
{
    public class FoodsController : Controller
    {
        private readonly FoodRepository _foodRepo;

        public FoodsController(FoodRepository foodRepo)
        {
            _foodRepo = foodRepo;
        }

        public async Task<IActionResult> Index()
        {
            var foods = await _foodRepo.GetAllFoodsAsync();
            return View(foods);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Food { GlycemicIndex = 50, CarbsPer100g = 10 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Food food)
        {
            if (ModelState.IsValid)
            {
                // Ensure name is clean
                food.Name = food.Name.Trim();
                
                // Set custom flag
                food.IsCustom = true;

                try
                {
                    await _foodRepo.AddFoodAsync(food);
                    TempData["SuccessMessage"] = $"Added custom food '{food.Name}' to your library.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    // Likely duplicate name
                    ModelState.AddModelError("Name", "A food item with this name already exists.");
                }
            }

            return View(food);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _foodRepo.DeleteFoodAsync(id);
            if (success)
            {
                TempData["SuccessMessage"] = "Custom food deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Cannot delete standard library food items.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult RecipeBuilder()
        {
            return View();
        }

        public class IngredientInput
        {
            public int FoodId { get; set; }
            public double AmountGrams { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRecipe(string recipeName, int servings, List<IngredientInput> ingredients)
        {
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                TempData["ErrorMessage"] = "Recipe Name is required.";
                return RedirectToAction(nameof(RecipeBuilder));
            }

            if (servings <= 0)
            {
                TempData["ErrorMessage"] = "Servings must be 1 or more.";
                return RedirectToAction(nameof(RecipeBuilder));
            }

            if (ingredients == null || ingredients.Count == 0 || ingredients.TrueForAll(i => i.AmountGrams <= 0))
            {
                TempData["ErrorMessage"] = "A recipe must contain at least one ingredient with a portion size greater than 0g.";
                return RedirectToAction(nameof(RecipeBuilder));
            }

            double totalCarbs = 0;
            double totalSugar = 0;
            double totalFiber = 0;
            double totalProtein = 0;
            double totalFat = 0;
            double totalCalories = 0;

            double weightedGiSum = 0;
            double totalCarbsForGiWeight = 0;

            foreach (var ing in ingredients)
            {
                if (ing.AmountGrams <= 0) continue;

                var food = await _foodRepo.GetFoodByIdAsync(ing.FoodId);
                if (food != null)
                {
                    double scale = ing.AmountGrams / 100.0;
                    double ingCarbs = food.CarbsPer100g * scale;
                    
                    totalCarbs += ingCarbs;
                    totalSugar += food.SugarPer100g * scale;
                    totalFiber += food.FiberPer100g * scale;
                    totalProtein += food.ProteinPer100g * scale;
                    totalFat += food.FatPer100g * scale;
                    totalCalories += food.CaloriesPer100g * scale;

                    if (food.GlycemicIndex > 0 && ingCarbs > 0)
                    {
                        weightedGiSum += food.GlycemicIndex * ingCarbs;
                        totalCarbsForGiWeight += ingCarbs;
                    }
                }
            }

            int finalGi = 0;
            if (totalCarbsForGiWeight > 0)
            {
                finalGi = (int)Math.Round(weightedGiSum / totalCarbsForGiWeight);
            }

            // Calculate per-serving values.
            // We store these in the per-100g database fields so that logging 100g of the food equals 1 serving.
            var compositeFood = new Food
            {
                Name = $"{recipeName.Trim()} (1 Serving)",
                GlycemicIndex = Math.Clamp(finalGi, 0, 100),
                CarbsPer100g = Math.Round(totalCarbs / servings, 2),
                SugarPer100g = Math.Round(totalSugar / servings, 2),
                FiberPer100g = Math.Round(totalFiber / servings, 2),
                ProteinPer100g = Math.Round(totalProtein / servings, 2),
                FatPer100g = Math.Round(totalFat / servings, 2),
                CaloriesPer100g = (int)Math.Round(totalCalories / servings),
                IsCustom = true
            };

            try
            {
                await _foodRepo.AddFoodAsync(compositeFood);
                TempData["SuccessMessage"] = $"Recipe '{recipeName}' compiled and saved as custom food '{compositeFood.Name}'.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = $"A food item named '{compositeFood.Name}' already exists.";
                return RedirectToAction(nameof(RecipeBuilder));
            }
        }
    }
}
