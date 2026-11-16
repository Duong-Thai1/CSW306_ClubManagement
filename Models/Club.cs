namespace ClubManagement.Models;

public sealed class Club
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRecruiting { get; set; }
    public int MaxMembers { get; set; }
}
