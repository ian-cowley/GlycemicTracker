using System.ComponentModel.DataAnnotations;

namespace GlycemicTracker.Models
{
    public class Food
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Range(0, 100)]
        [Display(Name = "Glycemic Index (GI)")]
        public int GlycemicIndex { get; set; }

        [Range(0, 100)]
        [Display(Name = "Carbohydrates per 100g")]
        public double CarbsPer100g { get; set; }

        [Range(0, 100)]
        [Display(Name = "Sugar per 100g")]
        public double SugarPer100g { get; set; }

        [Range(0, 100)]
        [Display(Name = "Fiber per 100g")]
        public double FiberPer100g { get; set; }

        [Range(0, 100)]
        [Display(Name = "Protein per 100g")]
        public double ProteinPer100g { get; set; }

        [Range(0, 100)]
        [Display(Name = "Fat per 100g")]
        public double FatPer100g { get; set; }

        [Range(0, 1000)]
        [Display(Name = "Calories per 100g (kcal)")]
        public int CaloriesPer100g { get; set; }

        public bool IsCustom { get; set; }

        [Display(Name = "Alcohol per 100g (g)")]
        public double AlcoholGrams { get; set; }
    }
}
