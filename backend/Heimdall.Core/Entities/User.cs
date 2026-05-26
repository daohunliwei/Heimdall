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

    [SugarColumn(ColumnName = "username", Length = 64)]
    public string Username { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "email", Length = 256, IsNullable = true)]
    public string? Email { get; set; }

    [SugarColumn(ColumnName = "password_hash", Length = 256, IsNullable = true)]
    public string? PasswordHash { get; set; }

    [SugarColumn(ColumnName = "source")]
    public UserSource Source { get; set; } = UserSource.Local;

    [SugarColumn(ColumnName = "role", Length = 16)]
    public string Role { get; set; } = "Viewer";

    [SugarColumn(ColumnName = "is_active")]
    public bool IsActive { get; set; } = true;

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(TaskRecord.UserId))]
    public List<TaskRecord> Tasks { get; set; } = new();
}
