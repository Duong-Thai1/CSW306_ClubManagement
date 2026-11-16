namespace ClubManagement.Models;

public sealed class RecruitmentPost
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ClubId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
}
