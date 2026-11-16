namespace ClubManagement.Models;

public sealed class EventProposal
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ClubId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }
    public string Status { get; set; } = string.Empty;
}
