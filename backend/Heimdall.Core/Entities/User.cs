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

    [SugarColumn(Length = 64)]
    public string Username { get; set; } = string.Empty;

    [SugarColumn(Length = 256, IsNullable = true)]
    public string? Email { get; set; }

    [SugarColumn(Length = 256, IsNullable = true)]
    public string? PasswordHash { get; set; }

    public UserSource Source { get; set; } = UserSource.Local;

    [SugarColumn(Length = 16)]
    public string Role { get; set; } = "Viewer";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(NavigateType.OneToMany, nameof(TaskRecord.UserId))]
    public List<TaskRecord> Tasks { get; set; } = new();
}
