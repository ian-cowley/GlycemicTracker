using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using GlycemicTracker.Models;

namespace GlycemicTracker.Data
{
    public class LogRepository
    {
        private readonly DatabaseHelper _db;

        public LogRepository(DatabaseHelper db)
        {
            _db = db;
        }

        #region Food Logs

        private FoodLog MapFoodLog(SqlDataReader reader)
        {
            return new FoodLog
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                FoodId = reader.GetInt32(reader.GetOrdinal("FoodId")),
                AmountGrams = (double)reader.GetDecimal(reader.GetOrdinal("AmountGrams")),
                LogTime = reader.GetDateTime(reader.GetOrdinal("LogTime")),
                CarbsGrams = (double)reader.GetDecimal(reader.GetOrdinal("CarbsGrams")),
                SugarGrams = (double)reader.GetDecimal(reader.GetOrdinal("SugarGrams")),
                FiberGrams = (double)reader.GetDecimal(reader.GetOrdinal("FiberGrams")),
                ProteinGrams = (double)reader.GetDecimal(reader.GetOrdinal("ProteinGrams")),
                FatGrams = (double)reader.GetDecimal(reader.GetOrdinal("FatGrams")),
                GlycemicLoad = (double)reader.GetDecimal(reader.GetOrdinal("GlycemicLoad")),
                FoodName = reader.GetString(reader.GetOrdinal("FoodName")),
                GlycemicIndex = reader.GetInt32(reader.GetOrdinal("GlycemicIndex")),
                AlcoholGrams = (double)reader.GetDecimal(reader.GetOrdinal("AlcoholGrams"))
            };
        }

        public async Task<List<FoodLog>> GetRecentFoodLogsAsync(int count)
        {
            var sql = $@"
                SELECT TOP {count} l.*, f.Name AS FoodName, f.GlycemicIndex
                FROM FoodLogs l
                JOIN Foods f ON l.FoodId = f.Id
                ORDER BY l.LogTime DESC";
            return await _db.ExecuteQueryAsync(sql, MapFoodLog);
        }

        public async Task<List<FoodLog>> GetFoodLogsForTimeRangeAsync(DateTime start, DateTime end)
        {
            var sql = @"
                SELECT l.*, f.Name AS FoodName, f.GlycemicIndex
                FROM FoodLogs l
                JOIN Foods f ON l.FoodId = f.Id
                WHERE l.LogTime >= @start AND l.LogTime <= @end
                ORDER BY l.LogTime ASC";
            var parameters = new[]
            {
                new SqlParameter("@start", start),
                new SqlParameter("@end", end)
            };
            return await _db.ExecuteQueryAsync(sql, MapFoodLog, parameters);
        }

        public async Task<int> AddFoodLogAsync(FoodLog log)
        {
            var sql = @"
                INSERT INTO FoodLogs (FoodId, AmountGrams, LogTime, CarbsGrams, SugarGrams, FiberGrams, ProteinGrams, FatGrams, GlycemicLoad, AlcoholGrams)
                VALUES (@FoodId, @AmountGrams, @LogTime, @CarbsGrams, @SugarGrams, @FiberGrams, @ProteinGrams, @FatGrams, @GlycemicLoad, @AlcoholGrams);
                SELECT CAST(scope_identity() AS INT);";

            var parameters = new[]
            {
                new SqlParameter("@FoodId", log.FoodId),
                new SqlParameter("@AmountGrams", log.AmountGrams),
                new SqlParameter("@LogTime", log.LogTime),
                new SqlParameter("@CarbsGrams", log.CarbsGrams),
                new SqlParameter("@SugarGrams", log.SugarGrams),
                new SqlParameter("@FiberGrams", log.FiberGrams),
                new SqlParameter("@ProteinGrams", log.ProteinGrams),
                new SqlParameter("@FatGrams", log.FatGrams),
                new SqlParameter("@GlycemicLoad", log.GlycemicLoad),
                new SqlParameter("@AlcoholGrams", log.AlcoholGrams)
            };

            var idResult = await _db.ExecuteScalarAsync(sql, parameters);
            if (idResult != null && idResult != DBNull.Value)
            {
                return (int)idResult;
            }
            return 0;
        }

        public async Task<bool> DeleteFoodLogAsync(int id)
        {
            var sql = "DELETE FROM FoodLogs WHERE Id = @id";
            var parameter = new SqlParameter("@id", id);
            int rows = await _db.ExecuteNonQueryAsync(sql, parameter);
            return rows > 0;
        }

        #endregion

        #region Glucose Readings

        private GlucoseReading MapGlucoseReading(SqlDataReader reader)
        {
            return new GlucoseReading
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                ReadingTime = reader.GetDateTime(reader.GetOrdinal("ReadingTime")),
                GlucoseValue = (double)reader.GetDecimal(reader.GetOrdinal("GlucoseValue")),
                Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes"))
            };
        }

        public async Task<List<GlucoseReading>> GetRecentReadingsAsync(int count)
        {
            var sql = $@"
                SELECT TOP {count} *
                FROM GlucoseReadings
                ORDER BY ReadingTime DESC";
            return await _db.ExecuteQueryAsync(sql, MapGlucoseReading);
        }

        public async Task<List<GlucoseReading>> GetReadingsForTimeRangeAsync(DateTime start, DateTime end)
        {
            var sql = @"
                SELECT *
                FROM GlucoseReadings
                WHERE ReadingTime >= @start AND ReadingTime <= @end
                ORDER BY ReadingTime ASC";
            var parameters = new[]
            {
                new SqlParameter("@start", start),
                new SqlParameter("@end", end)
            };
            return await _db.ExecuteQueryAsync(sql, MapGlucoseReading, parameters);
        }

        public async Task<int> AddGlucoseReadingAsync(GlucoseReading reading)
        {
            var sql = @"
                INSERT INTO GlucoseReadings (ReadingTime, GlucoseValue, Notes)
                VALUES (@ReadingTime, @GlucoseValue, @Notes);
                SELECT CAST(scope_identity() AS INT);";

            var parameters = new[]
            {
                new SqlParameter("@ReadingTime", reading.ReadingTime),
                new SqlParameter("@GlucoseValue", reading.GlucoseValue),
                new SqlParameter("@Notes", reading.Notes ?? (object)DBNull.Value)
            };

            var idResult = await _db.ExecuteScalarAsync(sql, parameters);
            if (idResult != null && idResult != DBNull.Value)
            {
                return (int)idResult;
            }
            return 0;
        }

        public async Task<bool> DeleteGlucoseReadingAsync(int id)
        {
            var sql = "DELETE FROM GlucoseReadings WHERE Id = @id";
            var parameter = new SqlParameter("@id", id);
            int rows = await _db.ExecuteNonQueryAsync(sql, parameter);
            return rows > 0;
        }

        #endregion
    }
}
