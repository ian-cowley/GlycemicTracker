using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using GlycemicTracker.Models;

namespace GlycemicTracker.Data
{
    public class SettingsRepository
    {
        private readonly DatabaseHelper _db;

        public SettingsRepository(DatabaseHelper db)
        {
            _db = db;
        }

        public async Task<List<GlycemicParameters>> GetAllParametersAsync()
        {
            var sql = "SELECT Id, StartDate, BaselineGlucose, InsulinResistanceFactor, DawnAmplitude FROM GlycemicParameters ORDER BY StartDate DESC";
            
            return await _db.ExecuteQueryAsync(sql, reader => new GlycemicParameters
            {
                Id = Convert.ToInt32(reader["Id"]),
                StartDate = Convert.ToDateTime(reader["StartDate"]),
                BaselineGlucose = Convert.ToDouble(reader["BaselineGlucose"]),
                InsulinResistanceFactor = Convert.ToDouble(reader["InsulinResistanceFactor"]),
                DawnAmplitude = Convert.ToDouble(reader["DawnAmplitude"])
            });
        }

        public async Task<int> AddParameterAsync(GlycemicParameters param)
        {
            var sql = @"
                INSERT INTO GlycemicParameters (StartDate, BaselineGlucose, InsulinResistanceFactor, DawnAmplitude)
                VALUES (@StartDate, @BaselineGlucose, @InsulinResistanceFactor, @DawnAmplitude)";

            var parameters = new[]
            {
                new SqlParameter("@StartDate", SqlDbType.Date) { Value = param.StartDate.Date },
                new SqlParameter("@BaselineGlucose", SqlDbType.Decimal) { Value = param.BaselineGlucose },
                new SqlParameter("@InsulinResistanceFactor", SqlDbType.Decimal) { Value = param.InsulinResistanceFactor },
                new SqlParameter("@DawnAmplitude", SqlDbType.Decimal) { Value = param.DawnAmplitude }
            };

            return await _db.ExecuteNonQueryAsync(sql, parameters);
        }

        public async Task<int> DeleteParameterAsync(int id)
        {
            // Do not delete the absolute oldest record to ensure there's always a base fallback parameters row
            var checkSql = "SELECT COUNT(*) FROM GlycemicParameters";
            var countResult = await _db.ExecuteScalarAsync(checkSql);
            int count = countResult != null ? Convert.ToInt32(countResult) : 0;

            if (count <= 1)
            {
                throw new InvalidOperationException("Cannot delete the only remaining sensitivity configuration record.");
            }

            // Find the oldest record ID
            var oldestSql = "SELECT TOP 1 Id FROM GlycemicParameters ORDER BY StartDate ASC";
            var oldestResult = await _db.ExecuteScalarAsync(oldestSql);
            int oldestId = oldestResult != null ? Convert.ToInt32(oldestResult) : 0;

            if (id == oldestId)
            {
                throw new InvalidOperationException("Cannot delete the baseline settings record. You can only delete newer adjustment overrides.");
            }

            var sql = "DELETE FROM GlycemicParameters WHERE Id = @Id";
            var parameters = new[]
            {
                new SqlParameter("@Id", SqlDbType.Int) { Value = id }
            };

            return await _db.ExecuteNonQueryAsync(sql, parameters);
        }
    }
}
