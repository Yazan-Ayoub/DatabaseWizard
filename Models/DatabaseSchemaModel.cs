using System.ComponentModel.DataAnnotations;

namespace DatabaseWizard.Models
{
    public class DatabaseSchemaModel
    {
        [Required(ErrorMessage = "Database name is required")]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "Database name must start with a letter and contain only letters, numbers, and underscores")]
        public string DatabaseName { get; set; } = string.Empty;

        public List<TableDefinition> Tables { get; set; } = new();
        public List<RelationDefinition> Relations { get; set; } = new();
    }

    public class TableDefinition
    {
        [Required(ErrorMessage = "Table name is required")]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "Table name must start with a letter and contain only letters, numbers, and underscores")]
        public string TableName { get; set; } = string.Empty;

        public List<ColumnDefinition> Columns { get; set; } = new();

        [Required(ErrorMessage = "Primary key is required")]
        public string PrimaryKey { get; set; } = string.Empty;
    }

    public class ColumnDefinition
    {
        [Required(ErrorMessage = "Column name is required")]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "Column name must start with a letter and contain only letters, numbers, and underscores")]
        public string ColumnName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Data type is required")]
        public string DataType { get; set; } = string.Empty;

        public bool IsNullable { get; set; } = false;
        
        public int? MaxLength { get; set; }
    }

    public class RelationDefinition
    {
        [Required(ErrorMessage = "Parent table is required")]
        public string ParentTable { get; set; } = string.Empty;

        [Required(ErrorMessage = "Parent column is required")]
        public string ParentColumn { get; set; } = string.Empty;

        [Required(ErrorMessage = "Child table is required")]
        public string ChildTable { get; set; } = string.Empty;

        [Required(ErrorMessage = "Child column is required")]
        public string ChildColumn { get; set; } = string.Empty;
    }

    public class DatabaseCreationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string GeneratedSql { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
    }
}
