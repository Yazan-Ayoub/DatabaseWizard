using DatabaseWizard.Models;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;

namespace DatabaseWizard.Services
{
    public interface ISqlGeneratorService
    {
        DatabaseCreationResult CreateDatabase(DatabaseSchemaModel schema);
        string GenerateSql(DatabaseSchemaModel schema);
    }

    public class SqlGeneratorService : ISqlGeneratorService
    {
        private readonly IConfiguration _configuration;
        private readonly HashSet<string> _validDataTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "INT", "BIGINT", "SMALLINT", "TINYINT",
            "VARCHAR", "NVARCHAR", "CHAR", "NCHAR", "TEXT", "NTEXT",
            "DECIMAL", "NUMERIC", "FLOAT", "REAL", "MONEY", "SMALLMONEY",
            "DATE", "DATETIME", "DATETIME2", "TIME", "DATETIMEOFFSET", "SMALLDATETIME",
            "BIT",
            "UNIQUEIDENTIFIER",
            "BINARY", "VARBINARY", "IMAGE"
        };

        public SqlGeneratorService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DatabaseCreationResult CreateDatabase(DatabaseSchemaModel schema)
        {
            var result = new DatabaseCreationResult();

            try
            {
                // Validate the schema
                var validationErrors = ValidateSchema(schema);
                if (validationErrors.Any())
                {
                    result.Success = false;
                    result.Errors = validationErrors;
                    result.Message = "Validation failed";
                    return result;
                }

                // Generate SQL
                var sql = GenerateSql(schema);
                result.GeneratedSql = sql;

                // Execute SQL
                ExecuteSql(schema.DatabaseName, sql);

                result.Success = true;
                result.Message = $"Database '{schema.DatabaseName}' created successfully!";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "Error creating database";
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        private List<string> ValidateSchema(DatabaseSchemaModel schema)
        {
            var errors = new List<string>();

            // Validate database name
            if (!IsValidIdentifier(schema.DatabaseName))
            {
                errors.Add("Invalid database name");
            }

            // Validate tables
            if (!schema.Tables.Any())
            {
                errors.Add("At least one table is required");
            }

            var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var table in schema.Tables)
            {
                // Check for duplicate table names
                if (!tableNames.Add(table.TableName))
                {
                    errors.Add($"Duplicate table name: {table.TableName}");
                }

                // Validate table name
                if (!IsValidIdentifier(table.TableName))
                {
                    errors.Add($"Invalid table name: {table.TableName}");
                }

                // Validate columns
                if (!table.Columns.Any())
                {
                    errors.Add($"Table '{table.TableName}' must have at least one column");
                }

                var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool primaryKeyFound = false;

                foreach (var column in table.Columns)
                {
                    // Check for duplicate column names
                    if (!columnNames.Add(column.ColumnName))
                    {
                        errors.Add($"Duplicate column name in table '{table.TableName}': {column.ColumnName}");
                    }

                    // Validate column name
                    if (!IsValidIdentifier(column.ColumnName))
                    {
                        errors.Add($"Invalid column name in table '{table.TableName}': {column.ColumnName}");
                    }

                    // Validate data type
                    var baseDataType = column.DataType.Split('(')[0].Trim();
                    if (!_validDataTypes.Contains(baseDataType))
                    {
                        errors.Add($"Invalid data type in table '{table.TableName}', column '{column.ColumnName}': {column.DataType}");
                    }

                    // Check if this is the primary key
                    if (column.ColumnName.Equals(table.PrimaryKey, StringComparison.OrdinalIgnoreCase))
                    {
                        primaryKeyFound = true;
                    }
                }

                // Validate primary key exists as a column
                if (!primaryKeyFound)
                {
                    errors.Add($"Primary key '{table.PrimaryKey}' not found in columns of table '{table.TableName}'");
                }
            }

            // Validate relations
            foreach (var relation in schema.Relations)
            {
                var parentTable = schema.Tables.FirstOrDefault(t => t.TableName.Equals(relation.ParentTable, StringComparison.OrdinalIgnoreCase));
                var childTable = schema.Tables.FirstOrDefault(t => t.TableName.Equals(relation.ChildTable, StringComparison.OrdinalIgnoreCase));

                if (parentTable == null)
                {
                    errors.Add($"Parent table '{relation.ParentTable}' not found in schema");
                }
                else if (!parentTable.Columns.Any(c => c.ColumnName.Equals(relation.ParentColumn, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"Parent column '{relation.ParentColumn}' not found in table '{relation.ParentTable}'");
                }

                if (childTable == null)
                {
                    errors.Add($"Child table '{relation.ChildTable}' not found in schema");
                }
                else if (!childTable.Columns.Any(c => c.ColumnName.Equals(relation.ChildColumn, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add($"Child column '{relation.ChildColumn}' not found in table '{relation.ChildTable}'");
                }
            }

            return errors;
        }

        public string GenerateSql(DatabaseSchemaModel schema)
        {
            var sb = new StringBuilder();

            // Create database
            sb.AppendLine($"CREATE DATABASE [{schema.DatabaseName}];");
            sb.AppendLine("GO");
            sb.AppendLine($"USE [{schema.DatabaseName}];");
            sb.AppendLine("GO");
            sb.AppendLine();

            // Create tables
            foreach (var table in schema.Tables)
            {
                sb.AppendLine($"CREATE TABLE [{table.TableName}] (");
                
                var columnDefinitions = new List<string>();
                
                foreach (var column in table.Columns)
                {
                    var colDef = $"    [{column.ColumnName}] {GetDataTypeDefinition(column)}";
                    
                    // Add NOT NULL constraint
                    if (!column.IsNullable)
                    {
                        colDef += " NOT NULL";
                    }
                    
                    columnDefinitions.Add(colDef);
                }
                
                // Add primary key constraint
                columnDefinitions.Add($"    CONSTRAINT [PK_{table.TableName}] PRIMARY KEY CLUSTERED ([{table.PrimaryKey}])");
                
                sb.AppendLine(string.Join(",\n", columnDefinitions));
                sb.AppendLine(");");
                sb.AppendLine("GO");
                sb.AppendLine();
            }

            // Create foreign key constraints
            foreach (var relation in schema.Relations)
            {
                var constraintName = $"FK_{relation.ChildTable}_{relation.ParentTable}";
                sb.AppendLine($"ALTER TABLE [{relation.ChildTable}]");
                sb.AppendLine($"    ADD CONSTRAINT [{constraintName}]");
                sb.AppendLine($"    FOREIGN KEY ([{relation.ChildColumn}])");
                sb.AppendLine($"    REFERENCES [{relation.ParentTable}] ([{relation.ParentColumn}]);");
                sb.AppendLine("GO");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GetDataTypeDefinition(ColumnDefinition column)
        {
            var dataType = column.DataType.ToUpper();
            
            // Handle data types that require length
            if ((dataType == "VARCHAR" || dataType == "NVARCHAR" || dataType == "CHAR" || dataType == "NCHAR" || 
                 dataType == "BINARY" || dataType == "VARBINARY") && column.MaxLength.HasValue)
            {
                if (column.MaxLength.Value == -1)
                {
                    return $"{dataType}(MAX)";
                }
                return $"{dataType}({column.MaxLength.Value})";
            }
            
            // Handle decimal/numeric
            if (dataType.StartsWith("DECIMAL") || dataType.StartsWith("NUMERIC"))
            {
                return column.DataType; // Keep as-is (e.g., DECIMAL(10,2))
            }
            
            return dataType;
        }

        private void ExecuteSql(string databaseName, string sql)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration");
            }

            // Split by GO statements
            var batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Where(batch => !string.IsNullOrWhiteSpace(batch))
                .ToList();

            using var connection = new SqlConnection(connectionString);
            connection.Open();

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;

                using var command = new SqlCommand(batch, connection);
                command.CommandTimeout = 60;
                command.ExecuteNonQuery();
            }
        }

        private bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;

            // Must start with letter or underscore, followed by letters, numbers, or underscores
            return Regex.IsMatch(identifier, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
        }
    }
}
