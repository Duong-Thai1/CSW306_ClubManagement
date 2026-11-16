namespace ClubManagement.Models;

public sealed class EventResult
{
    public string EventId { get; set; } = string.Empty;
    public string ConfirmedByUserId { get; set; } = string.Empty;
    public DateTime ConfirmedAt { get; set; }
}
