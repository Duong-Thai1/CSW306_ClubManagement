namespace ClubManagement.Models;

public sealed class AuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ActorId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
