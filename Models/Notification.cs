namespace ClubManagement.Models;

public sealed class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string? ClubId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
