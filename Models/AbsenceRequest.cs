namespace ClubManagement.Models;

public sealed class AbsenceRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MembershipId { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string? TaskAssignmentId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsLate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
}
