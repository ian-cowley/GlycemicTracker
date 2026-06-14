using System;
using System.ComponentModel.DataAnnotations;

namespace GlycemicTracker.Models
{
    public class GlycemicParameters
    {
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Effective From Date")]
        public DateTime StartDate { get; set; }

        [Required]
        [Range(3.0, 10.0)]
        [Display(Name = "Baseline Glucose (mmol/L)")]
        public double BaselineGlucose { get; set; }

        [Required]
        [Range(0.010, 0.500)]
        [Display(Name = "Insulin Resistance Factor")]
        public double InsulinResistanceFactor { get; set; }

        [Required]
        [Range(0.0, 5.0)]
        [Display(Name = "Dawn Surge Amplitude (mmol/L)")]
        public double DawnAmplitude { get; set; }
    }
}
