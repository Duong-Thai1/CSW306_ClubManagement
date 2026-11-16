namespace ClubManagement.Models;

public sealed class ClubTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ClubId { get; set; } = string.Empty;
    public string? AssigneeMemberId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Points { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Priority { get; set; } = string.Empty;
    public int? FrequencyDays { get; set; }
    public DateTime? RepeatUntil { get; set; }
    public string? TemplateId { get; set; }
    public DateTime? OccursAt { get; set; }
}
