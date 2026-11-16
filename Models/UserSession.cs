namespace ClubManagement.Models;

public sealed class UserSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string RefreshHash { get; set; } = string.Empty;
    public DateTime AccessExpiresAt { get; set; }
    public DateTime RefreshExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
