using SqlSugar;

namespace Heimdall.Core.Entities;

public enum UserSource
{
    Local = 0,
    Ldap = 1
}

[SugarTable("users")]
public class User
{
    [SugarColumn(IsPrimaryKey = true)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [SugarColumn(ColumnName = "Username", Length = 64)]
    public string Username { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "Email", Length = 256, IsNullable = true)]
    public string? Email { get; set; }

    [SugarColumn(ColumnName = "PasswordHash", Length = 256, IsNullable = true)]
    public string? PasswordHash { get; set; }

    [SugarColumn(ColumnName = "Source")]
    public UserSource Source { get; set; } = UserSource.Local;

    [SugarColumn(ColumnName = "Role", Length = 16)]
    public string Role { get; set; } = "Viewer";

    [SugarColumn(ColumnName = "IsActive")]
    public bool IsActive { get; set; } = true;

    [SugarColumn(ColumnName = "CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(TaskRecord.UserId))]
    public List<TaskRecord> Tasks { get; set; } = new();
}
