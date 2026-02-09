using DatabaseWizard.Models;
using DatabaseWizard.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace DatabaseWizard.Controllers
{
    public class DatabaseController : Controller
    {
        private readonly SqlGeneratorService _sqlGenerator;
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(SqlGeneratorService sqlGenerator, ILogger<DatabaseController> logger)
        {
            _sqlGenerator = sqlGenerator;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create([FromBody] DatabaseSchemaModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return Json(new DatabaseCreationResult
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                var result = _sqlGenerator.CreateDatabase(model);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database");
                return Json(new DatabaseCreationResult
                {
                    Success = false,
                    Message = "An unexpected error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost]
        public IActionResult Preview([FromBody] DatabaseSchemaModel model)
        {
            try
            {
                var validationErrors = ValidateModel(model);
                if (validationErrors.Any())
                {
                    return Json(new
                    {
                        success = false,
                        errors = validationErrors
                    });
                }

                // Generate SQL without executing
                var service = new SqlGeneratorService(HttpContext.RequestServices.GetRequiredService<IConfiguration>());
                var sql = GeneratePreviewSql(model);

                return Json(new
                {
                    success = true,
                    sql = sql
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    errors = new[] { ex.Message }
                });
            }
        }

        private List<string> ValidateModel(DatabaseSchemaModel model)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(model.DatabaseName))
            {
                errors.Add("Database name is required");
            }

            if (!model.Tables.Any())
            {
                errors.Add("At least one table is required");
            }

            return errors;
        }

        private string GeneratePreviewSql(DatabaseSchemaModel model)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"CREATE DATABASE [{model.DatabaseName}];");
            sb.AppendLine("GO");
            sb.AppendLine($"USE [{model.DatabaseName}];");
            sb.AppendLine("GO");
            sb.AppendLine();

            foreach (var table in model.Tables)
            {
                sb.AppendLine($"CREATE TABLE [{table.TableName}] (");
                
                var columnLines = new List<string>();
                foreach (var column in table.Columns)
                {
                    var nullableStr = column.IsNullable ? "" : " NOT NULL";
                    var dataType = column.DataType;
                    
                    if (column.MaxLength.HasValue && 
                        (dataType.Equals("VARCHAR", StringComparison.OrdinalIgnoreCase) || 
                         dataType.Equals("NVARCHAR", StringComparison.OrdinalIgnoreCase)))
                    {
                        dataType = column.MaxLength.Value == -1 
                            ? $"{dataType}(MAX)" 
                            : $"{dataType}({column.MaxLength.Value})";
                    }
                    
                    columnLines.Add($"    [{column.ColumnName}] {dataType}{nullableStr}");
                }
                
                columnLines.Add($"    CONSTRAINT [PK_{table.TableName}] PRIMARY KEY ([{table.PrimaryKey}])");
                sb.AppendLine(string.Join(",\n", columnLines));
                sb.AppendLine(");");
                sb.AppendLine("GO");
                sb.AppendLine();
            }

            foreach (var relation in model.Relations)
            {
                sb.AppendLine($"ALTER TABLE [{relation.ChildTable}]");
                sb.AppendLine($"    ADD CONSTRAINT [FK_{relation.ChildTable}_{relation.ParentTable}]");
                sb.AppendLine($"    FOREIGN KEY ([{relation.ChildColumn}])");
                sb.AppendLine($"    REFERENCES [{relation.ParentTable}] ([{relation.ParentColumn}]);");
                sb.AppendLine("GO");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
