using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DatabaseWizard.Models;
using DatabaseWizard.Services;
using System.Text.Json;

namespace DatabaseWizard.Pages;

public class IndexModel : PageModel
{
    private readonly IDatabaseExecutionService _databaseService;
    private readonly IConfiguration _configuration;

    public IndexModel(
        IDatabaseExecutionService databaseService,
        IConfiguration configuration)
    {
        _databaseService = databaseService;
        _configuration = configuration;
    }

    [BindProperty]
    public string SchemaJson { get; set; } = string.Empty;

    public DatabaseCreationResult? Result { get; set; }

    public void OnGet()
    {
        // Initialize with empty result
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SchemaJson))
            {
                Result = new DatabaseCreationResult
                {
                    Success = false,
                    Message = "No schema data provided"
                };
                return Page();
            }

            // Deserialize the schema
            var schema = JsonSerializer.Deserialize<DatabaseSchemaModel>(
                SchemaJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (schema == null)
            {
                Result = new DatabaseCreationResult
                {
                    Success = false,
                    Message = "Invalid schema format"
                };
                return Page();
            }

            // Get connection string from configuration
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                Result = new DatabaseCreationResult
                {
                    Success = false,
                    Message = "Database connection string not configured"
                };
                return Page();
            }

            // Create the database
            Result = await _databaseService.CreateDatabaseAsync(schema, connectionString);
        }
        catch (JsonException ex)
        {
            Result = new DatabaseCreationResult
            {
                Success = false,
                Message = $"Invalid JSON format: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            Result = new DatabaseCreationResult
            {
                Success = false,
                Message = $"Unexpected error: {ex.Message}"
            };
        }

        return Page();
    }
}
