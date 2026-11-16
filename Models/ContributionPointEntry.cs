namespace ClubManagement.Models;

public sealed class ContributionPointEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string MembershipId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public int Points { get; set; }
    public DateTime CreatedAt { get; set; }
}
