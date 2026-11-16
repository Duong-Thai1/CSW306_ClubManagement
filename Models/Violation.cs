namespace ClubManagement.Models;

public sealed class Violation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ClubMemberId { get; set; } = string.Empty;
    public string RecordedByUserId { get; set; } = string.Empty;
    public string? ClubTaskId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int PointsDeducted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AttendanceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResolutionReason { get; set; }
    public string? AppealReason { get; set; }
    public DateTime? AppealedAt { get; set; }
}
