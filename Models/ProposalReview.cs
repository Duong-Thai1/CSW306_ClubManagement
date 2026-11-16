namespace ClubManagement.Models;

public sealed class ProposalReview
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProposalId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string? Feedback { get; set; }
    public DateTime CreatedAt { get; set; }
}
