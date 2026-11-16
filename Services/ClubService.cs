using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class ClubService(ClubDbContext db, ClubAccess access, Actor actor, SqlTransactionLock locks,
    TimeProvider clock, IConfiguration config)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ClubDto>> ListAsync(string? search, string? fieldId, bool? recruiting, PageFilter page, CancellationToken ct)
    {
        var query = db.Set<Club>().AsNoTracking().Where(x => actor.IsStaff || x.Status == "Active");
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search));
        if (fieldId is not null) query = query.Where(x => x.Field == fieldId);
        if (recruiting.HasValue) query = query.Where(x => x.IsRecruiting == recruiting.Value);
        return await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(page.Offset).Take(page.PageSize)
            .Select(x => new ClubDto(x.Id, x.Name, x.Description, x.Field, x.Status, x.IsRecruiting, x.MaxMembers)).ToListAsync(ct);
    }

    public async Task<ClubDto> SaveAsync(string? id, SaveClubRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("memberships", ct);
        if (!await db.Set<ClubField>().AnyAsync(x => x.Id == input.FieldId, ct))
            throw ApiError.Bad("Unknown club field.");
        var club = id is null ? new Club { Status = "Pending", CreatedAt = Now } : await GetClubAsync(id, ct);
        if (id is null) db.Add(club);
        if (await db.Set<ClubMember>().CountAsync(x => x.ClubId == club.Id && x.Status == "Active", ct) > input.MaxMembers)
            throw ApiError.Conflict("Capacity is lower than the current membership count.");
        (club.Name, club.Description, club.Field, club.IsRecruiting, club.MaxMembers) =
            (input.Name.Trim(), input.Description, input.FieldId, input.IsRecruiting, input.MaxMembers);
        db.Audit(actor.Id, id is null ? "ClubCreated" : "ClubUpdated", club.Id, Now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(club.Id, club.Name, club.Description, club.Field, club.Status, club.IsRecruiting, club.MaxMembers);
    }

    public async Task ChangeStatusAsync(string id, string status, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("memberships", ct);
        var club = await GetClubAsync(id, ct);
        club.Status = status;
        db.Audit(actor.Id, "ClubStatus:" + status, id, Now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<FieldDto> CreateFieldAsync(FieldRequest input, CancellationToken ct)
    {
        var field = new ClubField { Name = input.Name.Trim() };
        db.Add(field);
        await db.SaveChangesAsync(ct);
        return new(field.Id, field.Name);
    }

    public async Task<IReadOnlyList<FieldDto>> FieldsAsync(CancellationToken ct) => await db.Set<ClubField>()
        .OrderBy(x => x.Name).Select(x => new FieldDto(x.Id, x.Name)).ToListAsync(ct);

    public async Task AssignOwnerAsync(string clubId, string userId, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("memberships", ct);
        var club = await GetClubAsync(clubId, ct);
        if (!await db.Set<ApplicationUser>().AnyAsync(x => x.Id == userId && x.IsActive && x.SystemRole == "Student", ct))
            throw ApiError.Bad("Choose an active student.");
        var existing = await db.Set<ClubMember>().SingleOrDefaultAsync(x => x.ClubId == clubId && x.UserId == userId, ct);
        if (existing?.Status == "Expelled") throw ApiError.Conflict("This user was expelled from the club.");
        if (existing?.Status != "Active") await CheckMembershipCapacityAsync(club, userId, ct);
        await db.Set<ClubMember>().Where(x => x.ClubId == clubId && x.UserId != userId && x.Role == "Owner" && x.Status == "Active")
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Role, "Member"), ct);
        ActivateMembership(existing, clubId, userId, "Owner");
        db.Audit(actor.Id, "ClubOwnerAssigned", clubId, Now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<JoinRequestDto> JoinAsync(string clubId, JoinClubRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("memberships", ct);
        var club = await GetClubAsync(clubId, ct);
        if (!club.IsRecruiting || club.Status != "Active") throw ApiError.Conflict("Recruitment is closed.");
        if (await db.Set<ClubMember>().AnyAsync(x => x.ClubId == clubId && x.UserId == actor.Id &&
            (x.Status == "Active" || x.Status == "Expelled"), ct))
            throw ApiError.Conflict("This student cannot apply to the club.");
        await CheckMembershipCapacityAsync(club, actor.Id, ct);
        if (await db.Set<JoinRequest>().AnyAsync(x => x.ClubId == clubId && x.UserId == actor.Id && x.Status == "Pending", ct))
            throw ApiError.Conflict("A join request is already pending.");
        var request = new JoinRequest { ClubId = clubId, UserId = actor.Id, Status = "Pending", CreatedAt = Now, Message = input.Message };
        db.Add(request);
        var ownerId = await db.Set<ClubMember>().Where(x => x.ClubId == clubId && x.Role == "Owner" && x.Status == "Active")
            .Select(x => x.UserId).SingleOrDefaultAsync(ct);
        if (ownerId is not null) db.Notify(ownerId, "New club application.", Now, clubId);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(request.Id, request.UserId, request.Message, request.Status, null);
    }

    public async Task<IReadOnlyList<JoinRequestDto>> JoinRequestsAsync(string clubId, PageFilter page, CancellationToken ct)
    {
        await access.RequireOwnerAsync(clubId, ct);
        return await db.Set<JoinRequest>().Where(x => x.ClubId == clubId).OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id).Skip(page.Offset).Take(page.PageSize)
            .Select(x => new JoinRequestDto(x.Id, x.UserId, x.Message, x.Status, x.ReviewReason)).ToListAsync(ct);
    }

    public async Task ReviewJoinAsync(string clubId, string requestId, ReviewJoinRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("memberships", ct);
        await access.RequireOwnerAsync(clubId, ct);
        var request = await db.Set<JoinRequest>().SingleOrDefaultAsync(x => x.Id == requestId && x.ClubId == clubId, ct)
            ?? throw ApiError.Missing("Join request not found.");
        if (request.Status != "Pending") throw ApiError.Conflict("This join request was already reviewed.");
        if (!input.Approve && string.IsNullOrWhiteSpace(input.Reason)) throw ApiError.Bad("A rejection requires a reason.");
        if (input.Approve)
        {
            var existing = await db.Set<ClubMember>().SingleOrDefaultAsync(x => x.ClubId == clubId && x.UserId == request.UserId, ct);
            if (existing?.Status is "Active" or "Expelled") throw ApiError.Conflict("This student cannot rejoin the club.");
            if (!await db.Set<ApplicationUser>().AnyAsync(x => x.Id == request.UserId && x.IsActive && x.SystemRole == "Student", ct))
                throw ApiError.Conflict("The student account is inactive.");
            await CheckMembershipCapacityAsync(await GetClubAsync(clubId, ct), request.UserId, ct);
            ActivateMembership(existing, clubId, request.UserId, "Member");
        }
        request.Status = input.Approve ? "Approved" : "Rejected";
        request.ReviewReason = input.Reason;
        request.ReviewedAt = Now;
        request.ReviewedByUserId = actor.Id;
        db.Notify(request.UserId, "Club application: " + request.Status, Now);
        db.Audit(actor.Id, "JoinRequest:" + request.Status, request.Id, Now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task LeaveAsync(string clubId, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("memberships", ct);
        var member = await access.MemberAsync(clubId, ct);
        if (member.Role == "Owner") throw ApiError.Conflict("Transfer club ownership before leaving.");
        member.Status = "Removed";
        db.Audit(actor.Id, "ClubLeft", clubId, Now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task ExpelAsync(string clubId, string memberId, ExpelMemberRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("memberships", ct);
        await locks.LockWithinTransactionAsync("points", ct);
        await access.RequireOwnerAsync(clubId, ct);
        var member = await db.Set<ClubMember>().SingleOrDefaultAsync(m => m.ClubId == clubId && m.Id == memberId &&
            m.Status == "Active" && m.Role == "Member", ct) ?? throw ApiError.Missing("Active member not found.");
        var now = Now;
        var semesterStart = new DateTime(now.Year, now.Month >= 7 ? 7 : 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var violations = await db.Set<Violation>().CountAsync(x =>
            x.ClubMemberId == memberId && x.Status == "Active" && x.CreatedAt >= semesterStart, ct);
        if (violations < config.GetValue("ClubRules:ExpulsionReviewThreshold", 5))
            throw ApiError.Conflict("The member does not meet the expulsion review threshold.");
        member.Status = "Expelled";
        db.Audit(actor.Id, "MembershipExpelled", memberId, now);
        db.Notify(member.UserId, $"Removed from the club: {input.Reason}", now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<RecruitmentPostDto> PostRecruitmentAsync(string clubId, RecruitmentPostRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("memberships", ct);
        await access.RequireOwnerAsync(clubId, ct);
        if (input.Deadline.UtcDateTime <= Now) throw ApiError.Bad("Recruitment deadline must be in the future.");
        var club = await GetClubAsync(clubId, ct);
        club.IsRecruiting = true;
        var post = new RecruitmentPost { ClubId = clubId, Title = input.Title, Content = input.Content, Deadline = input.Deadline.UtcDateTime };
        db.Add(post);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(post.Id, post.Title, post.Content, post.Deadline.Utc());
    }

    public async Task<IReadOnlyList<RecruitmentPostDto>> RecruitmentAsync(string clubId, PageFilter page, CancellationToken ct)
    {
        var club = await GetClubAsync(clubId, ct);
        if (club.Status != "Active") throw ApiError.Missing("Club not found.");
        var now = Now;
        return await db.Set<RecruitmentPost>().Where(x => x.ClubId == clubId && x.Deadline > now && club.IsRecruiting)
            .OrderBy(x => x.Deadline).ThenBy(x => x.Id).Skip(page.Offset).Take(page.PageSize)
            .Select(x => new RecruitmentPostDto(x.Id, x.Title, x.Content, x.Deadline.Utc())).ToListAsync(ct);
    }

    private async Task<Club> GetClubAsync(string id, CancellationToken ct) =>
        await db.Set<Club>().FindAsync([id], ct) ?? throw ApiError.Missing("Club not found.");

    private async Task CheckMembershipCapacityAsync(Club club, string userId, CancellationToken ct)
    {
        if (await db.Set<ClubMember>().CountAsync(x => x.ClubId == club.Id && x.Status == "Active", ct) >= club.MaxMembers)
            throw ApiError.Conflict("Club capacity reached.");
        var maximum = config.GetValue("ClubRules:MaxClubsPerStudent", 5);
        if (await db.Set<ClubMember>().CountAsync(x => x.UserId == userId && x.Status == "Active", ct) >= maximum)
            throw ApiError.Conflict("Student club limit reached.");
    }

    private void ActivateMembership(ClubMember? existing, string clubId, string userId, string role)
    {
        var member = existing ?? new ClubMember
        {
            ClubId = clubId, UserId = userId, JoinedAt = Now,
            Points = config.GetValue("ClubRules:InitialContributionPoints", 100)
        };
        member.Role = role;
        member.Status = "Active";
        if (existing is null) db.Add(member);
    }
}
