using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("system_settings")]
public class SystemSetting
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "Key", Length = 128)]
    public string Key { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "Value", ColumnDataType = "text")]
    public string Value { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "Description", ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
