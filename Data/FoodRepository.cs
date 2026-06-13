using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using CarbTracker.Models;

namespace CarbTracker.Data
{
    public class FoodRepository
    {
        private readonly DatabaseHelper _db;

        public FoodRepository(DatabaseHelper db)
        {
            _db = db;
        }

        private Food MapFood(SqlDataReader reader)
        {
            return new Food
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                GlycemicIndex = reader.GetInt32(reader.GetOrdinal("GlycemicIndex")),
                CarbsPer100g = (double)reader.GetDecimal(reader.GetOrdinal("CarbsPer100g")),
                SugarPer100g = (double)reader.GetDecimal(reader.GetOrdinal("SugarPer100g")),
                FiberPer100g = (double)reader.GetDecimal(reader.GetOrdinal("FiberPer100g")),
                ProteinPer100g = (double)reader.GetDecimal(reader.GetOrdinal("ProteinPer100g")),
                FatPer100g = (double)reader.GetDecimal(reader.GetOrdinal("FatPer100g")),
                CaloriesPer100g = reader.GetInt32(reader.GetOrdinal("CaloriesPer100g")),
                IsCustom = reader.GetBoolean(reader.GetOrdinal("IsCustom")),
                AlcoholGrams = (double)reader.GetDecimal(reader.GetOrdinal("AlcoholGrams"))
            };
        }

        public async Task<List<Food>> GetAllFoodsAsync()
        {
            var sql = "SELECT * FROM Foods ORDER BY Name ASC";
            return await _db.ExecuteQueryAsync(sql, MapFood);
        }

        public async Task<List<Food>> SearchFoodsAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<Food>();
            }

            // Normalise common misspellings for search matching
            string normalizedTerm = term.Trim();
            if (normalizedTerm.Contains("dortminder", StringComparison.OrdinalIgnoreCase) ||
                normalizedTerm.Contains("dormunder", StringComparison.OrdinalIgnoreCase))
            {
                normalizedTerm = "Dortmunder";
            }

            var sql = "SELECT TOP 10 * FROM Foods WHERE Name LIKE @term ORDER BY Name ASC";
            var parameter = new SqlParameter("@term", $"%{normalizedTerm}%");
            return await _db.ExecuteQueryAsync(sql, MapFood, parameter);
        }

        public async Task<Food?> GetFoodByIdAsync(int id)
        {
            var sql = "SELECT * FROM Foods WHERE Id = @id";
            var parameter = new SqlParameter("@id", id);
            return await _db.ExecuteQuerySingleAsync(sql, MapFood, parameter);
        }

        public async Task<int> AddFoodAsync(Food food)
        {
            var sql = @"
                INSERT INTO Foods (Name, GlycemicIndex, CarbsPer100g, SugarPer100g, FiberPer100g, ProteinPer100g, FatPer100g, CaloriesPer100g, IsCustom, AlcoholGrams)
                VALUES (@Name, @GI, @Carbs, @Sugar, @Fiber, @Protein, @Fat, @Calories, @IsCustom, @AlcoholGrams);
                SELECT CAST(scope_identity() AS INT);";

            var parameters = new[]
            {
                new SqlParameter("@Name", food.Name),
                new SqlParameter("@GI", food.GlycemicIndex),
                new SqlParameter("@Carbs", food.CarbsPer100g),
                new SqlParameter("@Sugar", food.SugarPer100g),
                new SqlParameter("@Fiber", food.FiberPer100g),
                new SqlParameter("@Protein", food.ProteinPer100g),
                new SqlParameter("@Fat", food.FatPer100g),
                new SqlParameter("@Calories", food.CaloriesPer100g),
                new SqlParameter("@IsCustom", food.IsCustom),
                new SqlParameter("@AlcoholGrams", food.AlcoholGrams)
            };

            var idResult = await _db.ExecuteScalarAsync(sql, parameters);
            if (idResult != null && idResult != DBNull.Value)
            {
                return (int)idResult;
            }
            return 0;
        }

        public async Task<bool> DeleteFoodAsync(int id)
        {
            var sql = "DELETE FROM Foods WHERE Id = @id AND IsCustom = 1";
            var parameter = new SqlParameter("@id", id);
            int rows = await _db.ExecuteNonQueryAsync(sql, parameter);
            return rows > 0;
        }
    }
}
