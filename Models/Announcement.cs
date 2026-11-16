namespace ClubManagement.Models;

public sealed class Announcement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ClubId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsImportant { get; set; }
    public DateTime CreatedAt { get; set; }
}
