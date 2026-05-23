using SqlSugar;

namespace Heimdall.Core.Entities;

[SugarTable("system_settings")]
public class SystemSetting
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(Length = 128)]
    public string Key { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text")]
    public string Value { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
