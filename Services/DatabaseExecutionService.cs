using Microsoft.Data.SqlClient;
using DatabaseWizard.Models;

namespace DatabaseWizard.Services;

public interface IDatabaseExecutionService
{
    Task<DatabaseCreationResult> CreateDatabaseAsync(DatabaseSchemaModel schema, string connectionString);
}

public class DatabaseExecutionService : IDatabaseExecutionService
{
    private readonly IValidationService _validationService;
    private readonly ISqlGeneratorService _sqlGenerator;

    public DatabaseExecutionService(
        IValidationService validationService,
        ISqlGeneratorService sqlGenerator)
    {
        _validationService = validationService;
        _sqlGenerator = sqlGenerator;
    }

    public async Task<DatabaseCreationResult> CreateDatabaseAsync(
        DatabaseSchemaModel schema, 
        string connectionString)
    {
        var result = new DatabaseCreationResult();

        try
        {
            // Validate schema
            var validationErrors = _validationService.ValidateSchema(schema);
            if (validationErrors.Any())
            {
                result.Success = false;
                result.Errors = validationErrors;
                result.Message = "Validation failed. Please check the errors.";
                return result;
            }

            // Generate SQL
            var sql = _sqlGenerator.GenerateSql(schema);
            result.GeneratedSql = sql;

            // Execute SQL
            await ExecuteSqlAsync(connectionString, sql);

            result.Success = true;
            result.Message = $"Database '{schema.DatabaseName}' created successfully!";
        }
        catch (SqlException ex)
        {
            result.Success = false;
            result.Message = $"SQL Error: {ex.Message}";
            result.Errors.Add($"Error Number: {ex.Number}");
            result.Errors.Add($"Error Message: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error: {ex.Message}";
            result.Errors.Add(ex.ToString());
        }

        return result;
    }

    private async Task ExecuteSqlAsync(string connectionString, string sql)
    {
        // Split by GO statements (SQL Server batch separator)
        var batches = sql.Split(new[] { "\nGO\n", "\nGO\r\n", "\r\nGO\r\n" }, 
            StringSplitOptions.RemoveEmptyEntries);

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var batch in batches)
        {
            var trimmedBatch = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmedBatch))
                continue;

            // Handle USE statement separately (needs to change database context)
            if (trimmedBatch.StartsWith("USE ", StringComparison.OrdinalIgnoreCase))
            {
                var dbName = ExtractDatabaseName(trimmedBatch);
                if (!string.IsNullOrEmpty(dbName))
                {
                    await connection.ChangeDatabaseAsync(dbName);
                }
                continue;
            }

            using var command = new SqlCommand(trimmedBatch, connection);
            command.CommandTimeout = 120; // 2 minutes timeout
            await command.ExecuteNonQueryAsync();
        }
    }

    private string ExtractDatabaseName(string useStatement)
    {
        // Extract database name from "USE [DatabaseName];" or "USE DatabaseName;"
        var match = System.Text.RegularExpressions.Regex.Match(
            useStatement, 
            @"USE\s+\[?(\w+)\]?", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
