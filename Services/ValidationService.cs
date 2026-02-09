using System.Text.RegularExpressions;

namespace DatabaseWizard.Services;

public interface IValidationService
{
    bool IsValidIdentifier(string identifier);
    List<string> ValidateSchema(Models.DatabaseSchemaModel schema);
}

public class ValidationService : IValidationService
{
    // SQL Server identifier rules: 1-128 chars, start with letter/underscore, contain letters/digits/underscores
    private static readonly Regex IdentifierRegex = new Regex(
        @"^[a-zA-Z_][a-zA-Z0-9_]{0,127}$",
        RegexOptions.Compiled
    );

    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TABLE",
        "DATABASE", "INDEX", "VIEW", "PROCEDURE", "FUNCTION", "TRIGGER", "USER",
        "SCHEMA", "GRANT", "REVOKE", "EXEC", "EXECUTE", "ORDER", "GROUP", "BY"
    };

    public bool IsValidIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        if (!IdentifierRegex.IsMatch(identifier))
            return false;

        if (ReservedWords.Contains(identifier))
            return false;

        return true;
    }

    public List<string> ValidateSchema(Models.DatabaseSchemaModel schema)
    {
        var errors = new List<string>();

        // Validate database name
        if (!IsValidIdentifier(schema.DatabaseName))
            errors.Add($"Invalid database name: '{schema.DatabaseName}'");

        // Validate tables
        if (schema.Tables == null || schema.Tables.Count == 0)
        {
            errors.Add("At least one table is required");
            return errors;
        }

        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in schema.Tables)
        {
            if (!IsValidIdentifier(table.TableName))
                errors.Add($"Invalid table name: '{table.TableName}'");
            
            if (!tableNames.Add(table.TableName))
                errors.Add($"Duplicate table name: '{table.TableName}'");

            if (table.Columns == null || table.Columns.Count == 0)
            {
                errors.Add($"Table '{table.TableName}' must have at least one column");
                continue;
            }

            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in table.Columns)
            {
                if (!IsValidIdentifier(column.ColumnName))
                    errors.Add($"Invalid column name: '{column.ColumnName}' in table '{table.TableName}'");
                
                if (!columnNames.Add(column.ColumnName))
                    errors.Add($"Duplicate column name: '{column.ColumnName}' in table '{table.TableName}'");

                if (string.IsNullOrWhiteSpace(column.DataType))
                    errors.Add($"Column '{column.ColumnName}' in table '{table.TableName}' must have a data type");
            }

            // Validate primary key exists
            if (!string.IsNullOrWhiteSpace(table.PrimaryKey))
            {
                if (!columnNames.Contains(table.PrimaryKey))
                    errors.Add($"Primary key '{table.PrimaryKey}' does not exist in table '{table.TableName}'");
            }
        }

        // Validate relations
        if (schema.Relations != null)
        {
            foreach (var relation in schema.Relations)
            {
                var parentTable = schema.Tables.FirstOrDefault(t => 
                    t.TableName.Equals(relation.ParentTable, StringComparison.OrdinalIgnoreCase));
                var childTable = schema.Tables.FirstOrDefault(t => 
                    t.TableName.Equals(relation.ChildTable, StringComparison.OrdinalIgnoreCase));

                if (parentTable == null)
                    errors.Add($"Relation references non-existent table: '{relation.ParentTable}'");
                
                if (childTable == null)
                    errors.Add($"Relation references non-existent table: '{relation.ChildTable}'");

                if (parentTable != null && !parentTable.Columns.Any(c => 
                    c.ColumnName.Equals(relation.ParentColumn, StringComparison.OrdinalIgnoreCase)))
                    errors.Add($"Relation references non-existent column: '{relation.ParentColumn}' in table '{relation.ParentTable}'");

                if (childTable != null && !childTable.Columns.Any(c => 
                    c.ColumnName.Equals(relation.ChildColumn, StringComparison.OrdinalIgnoreCase)))
                    errors.Add($"Relation references non-existent column: '{relation.ChildColumn}' in table '{relation.ChildTable}'");
            }
        }

        return errors;
    }
}
