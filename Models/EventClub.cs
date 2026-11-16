namespace ClubManagement.Models;

public sealed class EventClub
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EventId { get; set; } = string.Empty;
    public string ClubId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ExpectedMembers { get; set; }
    public string? Progress { get; set; }
}
