using ClubManagement.Data;
using ClubManagement.Models;

namespace ClubManagement.Services;

public sealed class ContributionLedger(ClubDbContext db)
{
    public void Add(ClubMember member, string sourceType, string sourceId, int points, DateTime now)
    {
        member.Points += points;
        db.Add(new ContributionPointEntry
        {
            MembershipId = member.Id, SourceType = sourceType, SourceId = sourceId, Points = points, CreatedAt = now
        });
    }
}
