using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace CarbTracker.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? @"Server=(localdb)\MSSQLLocalDB;Database=CarbTracker;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        }

        // Expose connection string for DB initialization
        public string ConnectionString => _connectionString;

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            {
                using (var command = new SqlCommand(sql, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }
                    await connection.OpenAsync();
                    return await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<object?> ExecuteScalarAsync(string sql, params SqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            {
                using (var command = new SqlCommand(sql, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }
                    await connection.OpenAsync();
                    return await command.ExecuteScalarAsync();
                }
            }
        }

        public async Task<List<T>> ExecuteQueryAsync<T>(string sql, Func<SqlDataReader, T> mapFunction, params SqlParameter[] parameters)
        {
            var results = new List<T>();
            using (var connection = GetConnection())
            {
                using (var command = new SqlCommand(sql, connection))
                {
                    if (parameters != null)
                    {
                        // Clone parameters to prevent "already contained by another SqlCommand" errors if reused
                        command.Parameters.AddRange(parameters);
                    }
                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(mapFunction(reader));
                        }
                    }
                }
            }
            return results;
        }

        public async Task<T?> ExecuteQuerySingleAsync<T>(string sql, Func<SqlDataReader, T> mapFunction, params SqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            {
                using (var command = new SqlCommand(sql, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }
                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return mapFunction(reader);
                        }
                    }
                }
            }
            return default;
        }
    }
}
