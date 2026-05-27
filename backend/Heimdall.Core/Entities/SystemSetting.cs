using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("system_settings")]
public class SystemSetting
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "key", Length = 128)]
    public string Key { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "value", ColumnDataType = "text")]
    public string Value { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "description", ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
