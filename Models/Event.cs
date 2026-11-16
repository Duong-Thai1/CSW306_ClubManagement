namespace ClubManagement.Models;

public sealed class Event
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? CreatedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public int RequiredClubCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? ClubId { get; set; }
    public string? ProposalId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int ActivityPoints { get; set; }
    public bool IsPublished { get; set; }
    public bool IsMandatory { get; set; }
    public DateTime? ResponseDeadline { get; set; }
    public int MinimumMembersPerClub { get; set; }
}
