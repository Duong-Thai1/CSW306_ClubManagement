namespace ClubManagement.Models;

public sealed class ActivityPointEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EventId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int Points { get; set; }
    public DateTime CreatedAt { get; set; }
}
