namespace ClubManagement.Models;

public sealed class TaskAssignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ClubTaskId { get; set; } = string.Empty;
    public string MembershipId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}
