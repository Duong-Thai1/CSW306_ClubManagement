namespace ClubManagement.Models;

public sealed class Attendance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EventId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool Present { get; set; }
    public string RecordedByUserId { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
