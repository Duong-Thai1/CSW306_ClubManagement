namespace ClubManagement.Models;

public sealed class ClubField
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
}
