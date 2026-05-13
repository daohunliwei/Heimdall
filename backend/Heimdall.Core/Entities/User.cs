namespace Heimdall.Core.Entities;

public enum UserSource
{
    Local = 0,
    Ldap = 1
}

public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public UserSource Source { get; set; } = UserSource.Local;
    public string Role { get; set; } = "Viewer";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<TaskRecord> Tasks { get; set; } = new List<TaskRecord>();
}
