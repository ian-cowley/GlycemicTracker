using System;

namespace GlycemicTracker.Models
{
    public class FoodLog
    {
        public int Id { get; set; }
        public int FoodId { get; set; }
        public double AmountGrams { get; set; }
        public DateTime LogTime { get; set; }
        public double CarbsGrams { get; set; }
        public double SugarGrams { get; set; }
        public double FiberGrams { get; set; }
        public double ProteinGrams { get; set; }
        public double FatGrams { get; set; }
        public double GlycemicLoad { get; set; }
        public double AlcoholGrams { get; set; }

        // Denormalized/join fields for display
        public string FoodName { get; set; } = string.Empty;
        public int GlycemicIndex { get; set; }
    }
}
