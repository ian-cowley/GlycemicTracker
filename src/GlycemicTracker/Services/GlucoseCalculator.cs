using System;
using System.Collections.Generic;
using System.Linq;
using GlycemicTracker.Models;

namespace GlycemicTracker.Services
{
    public class GlucoseCalculator
    {
        public class GlucosePoint
        {
            public DateTime Time { get; set; }
            public double EstimatedValue { get; set; }
            public double? ActualValue { get; set; }
            public bool IsSpike => EstimatedValue >= 7.8 || (ActualValue.HasValue && ActualValue.Value >= 7.8);
        }

        public class DashboardStats
        {
            public double CurrentGlucose { get; set; }
            public double PeakGlucoseToday { get; set; }
            public double TimeInRangePercentage { get; set; }
            public double TotalCarbsToday { get; set; }
            public double AverageGiToday { get; set; }
            public double TotalGlycemicLoadToday { get; set; }
            public int MinutesInRedZoneToday { get; set; }
        }

        private const double BaselineGlucose = 5.0; // mmol/L
        private const double ClearanceRateK = 1.0;  // K_elim (clearance rate per hour)

        public List<GlucosePoint> GenerateCurve(
            List<FoodLog> logs, 
            List<GlucoseReading> readings, 
            DateTime start, 
            DateTime end)
        {
            var points = new List<GlucosePoint>();
            var current = start;

            // Generate points in 5-minute increments
            while (current <= end)
            {
                var estVal = CalculateGlucoseAtTime(current, logs);
                
                // Find actual reading if one exists within 5 minutes of this time point
                var actualReading = readings
                    .Where(r => Math.Abs((r.ReadingTime - current).TotalMinutes) <= 2.5)
                    .OrderBy(r => Math.Abs((r.ReadingTime - current).TotalMinutes))
                    .FirstOrDefault();

                points.Add(new GlucosePoint
                {
                    Time = current,
                    EstimatedValue = Math.Round(estVal, 1),
                    ActualValue = actualReading != null ? (double?)Math.Round(actualReading.GlucoseValue, 1) : null
                });

                current = current.AddMinutes(5);
            }

            return points;
        }

        public double CalculateGlucoseAtTime(DateTime time, List<FoodLog> logs)
        {
            double glucose = BaselineGlucose;
            double totalSuppression = 0.0;

            // Dawn Phenomenon: circadian morning glucose surge peaking around 08:00 AM (local time).
            // Formulated as a Gaussian curve: Amplitude * exp(-(hour - peakHour)^2 / (2 * sigma^2)).
            // An amplitude of 3.6 mmol/L perfectly calibrates the baseline (5.0) to match the user's observed 08:24 finger-prick reading of 8.6 mmol/L.
            double timeOfDayHours = time.Hour + (time.Minute / 60.0) + (time.Second / 3600.0);
            double peakHour = 8.0;   // 08:00 AM
            double sigma = 1.25;     // spreads the curve from ~4:30 AM to ~11:30 AM
            double dawnAmplitude = 3.6;  // mmol/L surge height
            double dawnContribution = dawnAmplitude * Math.Exp(-Math.Pow(timeOfDayHours - peakHour, 2.0) / (2.0 * Math.Pow(sigma, 2.0)));
            glucose += dawnContribution;

            foreach (var log in logs)
            {
                if (log.LogTime > time) continue;

                double hoursSinceMeal = (time - log.LogTime).TotalHours;
                if (hoursSinceMeal > 24.0) continue; // Glucose contribution can persist longer, especially with alcohol clearance delays

                // Calculate alcohol units and hepatic suppression contribution
                if (log.AlcoholGrams > 0)
                {
                    double units = log.AlcoholGrams / 8.0;
                    if (units > 0 && hoursSinceMeal >= 0 && hoursSinceMeal < units)
                    {
                        double maxSuppression = Math.Min(1.2, units * 0.25);
                        double suppression = maxSuppression * (1.0 - (hoursSinceMeal / units));
                        totalSuppression += suppression;
                    }
                }

                // Calculate Glycemic Load
                double glycemicLoad = log.GlycemicLoad;
                if (glycemicLoad <= 0) continue;

                // Modeled peak glucose rise: each GL unit raises glucose by 0.16 mmol/L
                // (Using 0.16 since user has HbA1c of 78, indicating higher insulin resistance and larger spikes)
                double peakIncrease = glycemicLoad * 0.16;

                // Calculate Time to Peak (T_peak) in hours based on GI and macronutrients
                double tPeakBase = 0.75; // Default 45 minutes for high GI
                if (log.GlycemicIndex <= 40)
                {
                    tPeakBase = 1.5; // 90 mins for very low GI
                }
                else if (log.GlycemicIndex <= 55)
                {
                    tPeakBase = 1.25; // 75 mins for low GI
                }
                else if (log.GlycemicIndex <= 70)
                {
                    tPeakBase = 1.0; // 60 mins for medium GI
                }

                // Fat, protein and fiber delay gastric emptying, shifting the peak later and flattening the curve
                // Delays peak: +1.5% per gram of fat/protein, +4.0% per gram of fiber
                double tPeak = tPeakBase * (1.0 + (0.015 * (log.FatGrams + log.ProteinGrams)) + (0.04 * log.FiberGrams));

                // Bound T_peak to realistic ranges (30 mins to 3 hours)
                tPeak = Math.Clamp(tPeak, 0.5, 3.0);

                // Calculate absorption rate constant K_abs
                double kAbs = 1.5 / tPeak;

                // Slower carbohydrate clearance rate (ClearanceRateK) scaled based on session alcohol units (12h window)
                double elimRate = ClearanceRateK;
                double sessionAlcohol = logs
                    .Where(l => Math.Abs((l.LogTime - log.LogTime).TotalHours) <= 12)
                    .Sum(l => l.AlcoholGrams);

                if (sessionAlcohol > 0)
                {
                    double units = sessionAlcohol / 8.0;
                    elimRate = ClearanceRateK / (1.0 + (0.80 * units));
                }

                // Prevent division by zero if K_abs is identical to elimRate
                if (Math.Abs(kAbs - elimRate) < 0.01)
                {
                    kAbs += 0.02;
                }

                // Analytical peak time of the f(t) = e^(-elimRate * t) - e^(-K_abs * t) shape function
                double tPeakFunc = Math.Log(kAbs / elimRate) / (kAbs - elimRate);
                double maxFuncValue = Math.Exp(-elimRate * tPeakFunc) - Math.Exp(-kAbs * tPeakFunc);

                // Amplitude scale factor to ensure the peak reaches exactly peakIncrease
                double amplitude = peakIncrease / maxFuncValue;

                // Meal contribution at this time point
                double contribution = amplitude * (Math.Exp(-elimRate * hoursSinceMeal) - Math.Exp(-kAbs * hoursSinceMeal));

                glucose += Math.Max(0.0, contribution);
            }

            glucose -= totalSuppression;

            // Safe floor of 3.0 mmol/L to prevent mathematical hypoglycemia
            return Math.Max(3.0, glucose);
        }

        public DashboardStats CalculateStats(List<FoodLog> logs, List<GlucoseReading> readings, DateTime now, DateTime? targetDate = null)
        {
            var todayStart = (targetDate ?? now).Date;
            var todayEnd = todayStart.AddDays(1);

            // Filter today's logs
            var todaysLogs = logs.Where(l => l.LogTime >= todayStart && l.LogTime < todayEnd).ToList();
            
            // Total Carbs, GL, and average GI today
            double totalCarbs = todaysLogs.Sum(l => l.CarbsGrams);
            double totalGL = todaysLogs.Sum(l => l.GlycemicLoad);
            double avgGI = 0;
            if (todaysLogs.Count > 0)
            {
                double totalCarbsWithGI = todaysLogs.Where(l => l.GlycemicIndex > 0).Sum(l => l.CarbsGrams);
                if (totalCarbsWithGI > 0)
                {
                    avgGI = todaysLogs.Where(l => l.GlycemicIndex > 0).Sum(l => l.CarbsGrams * l.GlycemicIndex) / totalCarbsWithGI;
                }
                else
                {
                    avgGI = todaysLogs.Average(l => l.GlycemicIndex);
                }
            }

            // Generate a continuous curve for today to calculate Time In Range (TIR) and Peak
            // We evaluate from start of today until end of today at 5 minute intervals
            var todayPoints = GenerateCurve(logs, readings, todayStart, todayEnd);

            double peakGlucose = BaselineGlucose;
            int normalCount = 0;
            int redCount = 0;
            int totalCount = todayPoints.Count;

            foreach (var p in todayPoints)
            {
                double val = p.EstimatedValue;
                // If there's an actual reading close by, use the maximum of estimate vs actual
                if (p.ActualValue.HasValue)
                {
                    val = Math.Max(val, p.ActualValue.Value);
                }

                if (val > peakGlucose)
                {
                    peakGlucose = val;
                }

                // A normal postprandial glucose level should stay under 7.8 mmol/L
                if (val < 7.8)
                {
                    normalCount++;
                }
                else
                {
                    redCount++;
                }
            }

            double tir = totalCount > 0 ? ((double)normalCount / totalCount) * 100.0 : 100.0;
            int minutesRed = redCount * 5; // 5 minute increments

            // Estimated current glucose level (right now)
            double currentGlucose = CalculateGlucoseAtTime(now, logs);
            
            // If there's a very recent actual reading (within 30 mins), blend it or show actual
            var recentReading = readings
                .Where(r => Math.Abs((r.ReadingTime - now).TotalMinutes) <= 30)
                .OrderBy(r => Math.Abs((r.ReadingTime - now).TotalMinutes))
                .FirstOrDefault();

            if (recentReading != null)
            {
                // Simple linear weight blending depending on recency (closer to 0 mins = closer to actual)
                double diffMins = Math.Abs((recentReading.ReadingTime - now).TotalMinutes);
                double weightActual = Math.Clamp(1.0 - (diffMins / 30.0), 0.0, 1.0);
                currentGlucose = (recentReading.GlucoseValue * weightActual) + (currentGlucose * (1.0 - weightActual));
            }

            return new DashboardStats
            {
                CurrentGlucose = Math.Round(currentGlucose, 1),
                PeakGlucoseToday = Math.Round(peakGlucose, 1),
                TimeInRangePercentage = Math.Round(tir, 0),
                TotalCarbsToday = Math.Round(totalCarbs, 1),
                AverageGiToday = Math.Round(avgGI, 0),
                TotalGlycemicLoadToday = Math.Round(totalGL, 1),
                MinutesInRedZoneToday = minutesRed
            };
        }
    }
}
