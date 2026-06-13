using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GlycemicTracker.Models;
using GlycemicTracker.Data;
using GlycemicTracker.Services;
using TinyPdf;

namespace GlycemicTracker.Controllers
{
    public class HomeController : Controller
    {
        private readonly FoodRepository _foodRepo;
        private readonly LogRepository _logRepo;
        private readonly GlucoseCalculator _calculator;

        public HomeController(
            FoodRepository foodRepo, 
            LogRepository logRepo, 
            GlucoseCalculator calculator)
        {
            _foodRepo = foodRepo;
            _logRepo = logRepo;
            _calculator = calculator;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var start = now.Date;
            var end = start.AddDays(1);

            // Fetch today's food logs and glucose readings
            var logs = await _logRepo.GetFoodLogsForTimeRangeAsync(start.AddDays(-1), end.AddDays(1)); // fetch extra for boundary calculations
            var readings = await _logRepo.GetReadingsForTimeRangeAsync(start.AddDays(-1), end.AddDays(1));

            var stats = _calculator.CalculateStats(logs, readings, now);
            var recentLogs = await _logRepo.GetRecentFoodLogsAsync(10);
            var recentReadings = await _logRepo.GetRecentReadingsAsync(10);

            ViewBag.Stats = stats;
            ViewBag.RecentLogs = recentLogs;
            ViewBag.RecentReadings = recentReadings;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SearchFoods(string term)
        {
            var results = await _foodRepo.SearchFoodsAsync(term);
            var autocompleteData = results.Select(f => new
            {
                id = f.Id,
                label = $"{f.Name} (GI: {f.GlycemicIndex}, {f.CarbsPer100g}g Carbs/100g)",
                value = f.Name,
                gi = f.GlycemicIndex,
                carbs = f.CarbsPer100g,
                sugar = f.SugarPer100g,
                fiber = f.FiberPer100g,
                protein = f.ProteinPer100g,
                fat = f.FatPer100g,
                calories = f.CaloriesPer100g
            });
            return Json(autocompleteData);
        }

        [HttpGet]
        public async Task<IActionResult> GetGlucoseChartData(string timeframe = "Today")
        {
            var now = DateTime.Now;
            DateTime start;
            DateTime end;

            switch (timeframe)
            {
                case "Last24Hours":
                    start = now.AddHours(-24);
                    end = now.AddHours(4); // predict 4 hours in the future
                    break;
                case "Yesterday":
                    start = now.Date.AddDays(-1);
                    end = now.Date;
                    break;
                case "Last7Days":
                    start = now.Date.AddDays(-7);
                    end = now.Date.AddDays(1);
                    break;
                case "Today":
                default:
                    start = now.Date;
                    end = now.Date.AddDays(1);
                    break;
            }

            // Fetch logs and readings for calculation (pad by 1 day on each side for overlap math)
            var logs = await _logRepo.GetFoodLogsForTimeRangeAsync(start.AddDays(-1), end.AddDays(1));
            var readings = await _logRepo.GetReadingsForTimeRangeAsync(start.AddDays(-1), end.AddDays(1));

            var points = _calculator.GenerateCurve(logs, readings, start, end);
            var stats = _calculator.CalculateStats(logs, readings, now);

            return Json(new
            {
                points = points.Select(p => new
                {
                    time = p.Time.ToString("yyyy-MM-ddTHH:mm:ss"),
                    displayTime = p.Time.ToString("HH:mm"),
                    displayDate = p.Time.ToString("MMM dd HH:mm"),
                    estimatedValue = p.EstimatedValue,
                    actualValue = p.ActualValue,
                    isSpike = p.IsSpike
                }),
                stats = stats
            });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadReport()
        {
            var now = DateTime.Now;
            var start = now.Date.AddDays(-7);
            var end = now.Date.AddDays(1);

            // Fetch last 7 days of logs & readings
            var logs = await _logRepo.GetFoodLogsForTimeRangeAsync(start, end);
            var readings = await _logRepo.GetReadingsForTimeRangeAsync(start, end);

            // Calculate Today's (24h) stats using the existing stats calculator
            var statsToday = _calculator.CalculateStats(logs, readings, now);

            // Calculate 7-Day statistics
            var points7Days = _calculator.GenerateCurve(logs, readings, start, end);
            
            double peak7Days = 5.0; // baseline
            double sumGlucose7Days = 0;
            int normalCount7Days = 0;
            int totalCount7Days = points7Days.Count;

            foreach (var p in points7Days)
            {
                double val = p.EstimatedValue;
                if (p.ActualValue.HasValue)
                {
                    val = Math.Max(val, p.ActualValue.Value);
                }

                sumGlucose7Days += val;
                
                if (val > peak7Days)
                {
                    peak7Days = val;
                }

                if (val < 7.8)
                {
                    normalCount7Days++;
                }
            }

            double avgGlucose7Days = totalCount7Days > 0 ? sumGlucose7Days / totalCount7Days : 5.0;
            double tir7Days = totalCount7Days > 0 ? ((double)normalCount7Days / totalCount7Days) * 100.0 : 100.0;
            
            double totalCarbs7Days = logs.Sum(l => l.CarbsGrams);
            double avgDailyCarbs7Days = totalCarbs7Days / 7.0;
            
            double avgGi7Days = 0;
            double totalCarbsWithGI7Days = logs.Where(l => l.GlycemicIndex > 0).Sum(l => l.CarbsGrams);
            if (totalCarbsWithGI7Days > 0)
            {
                avgGi7Days = logs.Where(l => l.GlycemicIndex > 0).Sum(l => l.CarbsGrams * l.GlycemicIndex) / totalCarbsWithGI7Days;
            }
            else if (logs.Any())
            {
                avgGi7Days = logs.Average(l => l.GlycemicIndex);
            }
            
            int spikeCount7Days = readings.Count(r => r.GlucoseValue >= 7.8);
            
            // Generate Markdown string
            var sb = new StringBuilder();
            sb.AppendLine("# GLYCEMIC PROGRESS REPORT");
            sb.AppendLine();
            sb.AppendLine($"**Generated on:** {now:MMMM dd, yyyy HH:mm}");
            sb.AppendLine("**Patient:** Cowley");
            sb.AppendLine("**Target HbA1c:** < 48 mmol/mol");
            sb.AppendLine("**Target Glucose Range:** 4.0 - 7.8 mmol/L");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 1. Executive Summary (Last 7 Days)");
            sb.AppendLine();
            sb.AppendLine($"* **Average Estimated Glucose:** {avgGlucose7Days:0.0} mmol/L");
            sb.AppendLine($"* **Peak Glucose:** {peak7Days:0.0} mmol/L");
            sb.AppendLine($"* **Time in Range (TIR):** {tir7Days:0}% (Target: >= 70%)");
            sb.AppendLine($"* **Average Daily Carbohydrates:** {avgDailyCarbs7Days:0.0}g");
            sb.AppendLine($"* **Average Meal Glycemic Index (GI):** {avgGi7Days:0}");
            sb.AppendLine($"* **Recorded Glycemic Spikes (>= 7.8 mmol/L):** {spikeCount7Days}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 2. Today's Summary (Last 24 Hours)");
            sb.AppendLine();
            sb.AppendLine($"* **Current Estimated Glucose:** {statsToday.CurrentGlucose:0.0} mmol/L");
            sb.AppendLine($"* **Peak Glucose Today:** {statsToday.PeakGlucoseToday:0.0} mmol/L");
            sb.AppendLine($"* **Time in Range Today:** {statsToday.TimeInRangePercentage:0}%");
            sb.AppendLine($"* **Time Above Range (Red Zone):** {statsToday.MinutesInRedZoneToday} minutes");
            sb.AppendLine($"* **Total Carbs Consumed Today:** {statsToday.TotalCarbsToday:0.0}g");
            sb.AppendLine($"* **Average GI Today:** {statsToday.AverageGiToday:0}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 3. Chronological Log (Last 7 Days)");
            
            var events = new List<(DateTime Time, string Type, string Description, string Value, string Status)>();
            
            foreach (var log in logs)
            {
                events.Add((
                    log.LogTime,
                    "Food",
                    $"{log.FoodName} ({log.AmountGrams:0}g portion)",
                    $"{log.CarbsGrams:0.0}g Carbs, GL: {log.GlycemicLoad:0.0}",
                    log.GlycemicLoad > 19 ? "High GL" : log.GlycemicLoad > 10 ? "Med GL" : "Low GL"
                ));
            }

            foreach (var reading in readings)
            {
                events.Add((
                    reading.ReadingTime,
                    "Reading",
                    "Blood glucose measurement",
                    $"{reading.GlucoseValue:0.0} mmol/L",
                    reading.GlucoseValue >= 7.8 ? "Spike" : "Normal"
                ));
            }

            var sortedEvents = events.OrderByDescending(e => e.Time).ToList();

            if (!sortedEvents.Any())
            {
                sb.AppendLine();
                sb.AppendLine("No entries recorded in the last 7 days.");
            }
            else
            {
                DateTime? lastDate = null;
                foreach (var ev in sortedEvents)
                {
                    if (lastDate == null || lastDate.Value.Date != ev.Time.Date)
                    {
                        lastDate = ev.Time.Date;
                        sb.AppendLine();
                        sb.AppendLine($"### {lastDate.Value:dddd, MMMM dd, yyyy}");
                        sb.AppendLine();
                    }
                    
                    string alertMarker = ev.Status == "Spike" || ev.Status == "High GL" ? " [ALERT]" : "";
                    sb.AppendLine($"* **{ev.Time:HH:mm}** - **{ev.Type}**: {ev.Description} | {ev.Value}{alertMarker}");
                }
            }

            // Convert Markdown to PDF
            var options = new TinyPdfCreate.MarkdownOptions(Compress: true);
            byte[] pdfBytes = TinyPdfCreate.Markdown(sb.ToString(), options);

            return File(pdfBytes, "application/pdf", $"GlycemicTracker_Doctor_Report_{now:yyyyMMdd}.pdf");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
