using System;
using System.ComponentModel.DataAnnotations;

namespace GlycemicTracker.Models
{
    public class GlucoseReading
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Time of Reading")]
        public DateTime ReadingTime { get; set; }

        [Required]
        [Range(1.0, 35.0)]
        [Display(Name = "Glucose Level (mmol/L)")]
        public double GlucoseValue { get; set; }

        [StringLength(250)]
        public string? Notes { get; set; }
    }
}
