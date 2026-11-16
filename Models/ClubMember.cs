namespace ClubManagement.Models;

public sealed class ClubMember
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ClubId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Points { get; set; }
    public DateTime JoinedAt { get; set; }
}
