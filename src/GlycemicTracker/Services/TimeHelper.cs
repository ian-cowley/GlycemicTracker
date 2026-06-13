using System;

namespace GlycemicTracker.Services
{
    public static class TimeHelper
    {
        private static readonly TimeZoneInfo UkTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

        public static DateTime UkNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, UkTimeZone);

        public static DateTime ToUkTime(DateTime utcDateTime)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, UkTimeZone);
        }
    }
}
