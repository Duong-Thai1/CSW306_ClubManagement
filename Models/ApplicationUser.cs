namespace ClubManagement.Models;

public sealed class ApplicationUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
    public string SystemRole { get; set; } = string.Empty;
}
