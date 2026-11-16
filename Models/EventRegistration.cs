namespace ClubManagement.Models;

public sealed class EventRegistration
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EventId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
