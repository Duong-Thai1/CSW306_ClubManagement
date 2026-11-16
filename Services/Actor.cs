using ClubManagement.Data;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class Actor(IHttpContextAccessor context)
{
    public string Id => context.HttpContext?.User.FindFirst("sub")?.Value ?? throw ApiError.Unauthorized();
    public string SessionId => context.HttpContext?.User.FindFirst("sid")?.Value ?? throw ApiError.Unauthorized();
    public bool IsStaff => context.HttpContext?.User.IsInRole("Staff") == true;
}

public sealed class ClubAccess(ClubDbContext db, Actor actor)
{
    public async Task<ClubMember> MemberAsync(string clubId, CancellationToken ct) =>
        await db.Set<ClubMember>().SingleOrDefaultAsync(x => x.ClubId == clubId &&
            x.UserId == actor.Id && x.Status == "Active" &&
            db.Set<Club>().Any(c => c.Id == x.ClubId && c.Status == "Active"), ct) ?? throw ApiError.Forbidden();

    public async Task RequireOwnerAsync(string clubId, CancellationToken ct)
    {
        var member = await MemberAsync(clubId, ct);
        if (member.Role != "Owner")
            throw ApiError.Forbidden();
    }
}
