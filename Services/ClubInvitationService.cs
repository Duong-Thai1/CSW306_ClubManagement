using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class ClubInvitationService(ClubDbContext db, EventAccess events, ClubAccess access, SqlTransactionLock locks,
    Actor actor, TimeProvider clock, IConfiguration config)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task InviteAsync(string eventId, InviteClubsRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync(EventAccess.LockKey(eventId), ct);
        var item = await GetStaffEventAsync(eventId, ct);
        var now = Now;
        if (item.ResponseDeadline <= now) throw ApiError.Conflict("The invitation deadline has passed.");
        var ids = input.ClubIds?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        if (!input.AllActive && ids.Length == 0) throw ApiError.Bad("Select clubs or use AllActive.");
        var active = db.Set<Club>().Where(x => x.Status == "Active");
        if (!input.AllActive) active = active.Where(x => ids.Contains(x.Id));
        var selected = await active.Select(x => x.Id).ToListAsync(ct);
        if (!input.AllActive && selected.Count != ids.Length) throw ApiError.Bad("All selected clubs must be active.");
        var existing = await db.Set<EventClub>().Where(x => x.EventId == eventId).Select(x => x.ClubId).ToListAsync(ct);
        var newClubs = selected.Except(existing, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var id in newClubs) db.Add(new EventClub
            { EventId = eventId, ClubId = id, Status = "Pending", CreatedAt = now, UpdatedAt = now });
        var owners = await db.Set<ClubMember>().Where(m => newClubs.Contains(m.ClubId) && m.Role == "Owner" && m.Status == "Active").ToListAsync(ct);
        foreach (var owner in owners) db.Notify(owner.UserId, "Invitation: " + item.Title, now, owner.ClubId);
        db.Audit(actor.Id, "ClubsInvited", eventId, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<InvitationDto> RespondAsync(string eventId, string clubId, InvitationResponseRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync(EventAccess.LockKey(eventId), ct);
        await access.RequireOwnerAsync(clubId, ct);
        var item = await GetStaffEventAsync(eventId, ct);
        var invite = await GetInvitationAsync(eventId, clubId, ct);
        if (item.ResponseDeadline <= Now) throw ApiError.Conflict("The invitation deadline has passed.");
        if (input.Accept && (input.ExpectedMembers is null || input.ExpectedMembers < item.MinimumMembersPerClub))
            throw ApiError.Bad("Specify an expected headcount meeting the event minimum.");
        if (!input.Accept && string.IsNullOrWhiteSpace(input.Reason)) throw ApiError.Bad("A reason is required when declining.");
        invite.Status = input.Accept ? "Accepted" : "Declined";
        invite.ExpectedMembers = input.Accept ? input.ExpectedMembers : null;
        invite.Note = input.Accept ? "" : input.Reason!;
        if (!input.Accept) invite.Progress = null;
        invite.UpdatedAt = Now;
        if (input.Accept)
        {
            var members = await db.Set<ClubMember>().Where(x => x.ClubId == clubId && x.Status == "Active").Select(x => x.UserId).ToListAsync(ct);
            foreach (var member in members) db.Notify(member, "Club accepted invitation: " + item.Title, Now, clubId);
        }
        db.Audit(actor.Id, "Invitation:" + invite.Status, invite.Id, Now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(invite, item, Now);
    }

    public async Task<InvitationDto> SetProgressAsync(string eventId, string clubId, UpdateProgressRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync(EventAccess.LockKey(eventId), ct);
        await access.RequireOwnerAsync(clubId, ct);
        var item = await GetStaffEventAsync(eventId, ct);
        var invite = await GetInvitationAsync(eventId, clubId, ct);
        if (invite.Status != "Accepted" || (invite.Progress == "Completed" && input.Progress != "Completed"))
            throw ApiError.Conflict("Progress cannot be changed.");
        if (input.Progress == "Completed" && Now < (item.EndTime ?? item.StartTime))
            throw ApiError.Conflict("The event has not finished.");
        invite.Progress = input.Progress;
        invite.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(invite, item, Now);
    }

    public async Task<IReadOnlyList<InvitationDto>> ForClubAsync(string clubId, CancellationToken ct)
    {
        await access.RequireOwnerAsync(clubId, ct);
        var entries = await (from invite in db.Set<EventClub>()
                             join item in db.Set<Event>() on invite.EventId equals item.Id
                             where invite.ClubId == clubId && item.Kind == "Staff"
                             orderby item.StartTime, invite.Id
                             select new { Invite = invite, Item = item }).ToListAsync(ct);
        var now = Now;
        return entries.Select(x => ToDto(x.Invite, x.Item, now)).ToArray();
    }

    public async Task<EventProgressDto> ProgressAsync(string eventId, CancellationToken ct)
    {
        var item = await GetStaffEventAsync(eventId, ct);
        var invitations = await db.Set<EventClub>().Where(x => x.EventId == eventId).OrderBy(x => x.ClubId).ToListAsync(ct);
        var now = Now;
        return new(invitations.Count, invitations.Count(x => x.Status == "Accepted"),
            invitations.Count(x => x.Progress == "Completed"), item.RequiredClubCount,
            invitations.Select(x => ToDto(x, item, now)).ToArray());
    }

    public async Task<IReadOnlyList<RankedClubDto>> RecommendAsync(string eventId, int count, CancellationToken ct)
    {
        var item = await GetStaffEventAsync(eventId, ct);
        var fields = await db.Set<EventClubFieldRelation>().Where(x => x.EventId == eventId).Select(x => x.Field).ToListAsync(ct);
        var warningThreshold = config.GetValue("ClubRules:WarningThreshold", 3);
        var today = Now;
        var semesterStart = new DateTime(today.Year, today.Month >= 7 ? 7 : 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var candidates = await db.Set<Club>().Where(x => x.Status == "Active")
            .Where(x => !db.Set<ClubMember>().Any(m => m.ClubId == x.Id && m.Status == "Active" &&
                db.Set<Violation>().Count(v => v.ClubMemberId == m.Id && v.Status == "Active" && v.CreatedAt >= semesterStart) >= warningThreshold))
            .Select(x => new
            {
                x.Id, x.Name, x.Field,
                Members = db.Set<ClubMember>().Count(m => m.ClubId == x.Id && m.Status == "Active" &&
                    db.Set<ApplicationUser>().Any(u => u.Id == m.UserId && u.IsActive)),
                Accepted = db.Set<EventClub>().Count(i => i.ClubId == x.Id && i.Status == "Accepted" && i.EventId != eventId &&
                    db.Set<Event>().Any(e => e.Id == i.EventId && e.EndTime <= today)),
                Completed = db.Set<EventClub>().Count(i => i.ClubId == x.Id && i.Status == "Accepted" && i.Progress == "Completed" &&
                    i.EventId != eventId && db.Set<Event>().Any(e => e.Id == i.EventId && e.EndTime <= today))
            }).ToListAsync(ct);
        var fieldWeight = config.GetValue("ClubRules:Recommendation:FieldWeight", 0.4);
        var membersWeight = config.GetValue("ClubRules:Recommendation:MembersWeight", 0.3);
        var completionWeight = config.GetValue("ClubRules:Recommendation:CompletionWeight", 0.3);
        var threshold = config.GetValue("ClubRules:Recommendation:MinimumScore", 0.0);
        return candidates.Select(x =>
        {
            var fieldMatches = fields.Count == 0 || fields.Contains(x.Field, StringComparer.OrdinalIgnoreCase);
            var completion = x.Accepted == 0 ? 0.0 : (double)x.Completed / x.Accepted;
            var score = (fieldMatches ? fieldWeight : 0) + (x.Members >= item.MinimumMembersPerClub ? membersWeight : 0) +
                        completionWeight * completion;
            return new RankedClubDto(x.Id, x.Name, score, fieldMatches, x.Members, completion);
        }).Where(x => x.Score >= threshold).OrderByDescending(x => x.Score).ThenBy(x => x.ClubId).Take(count).ToArray();
    }

    private async Task<Event> GetStaffEventAsync(string id, CancellationToken ct)
    {
        var item = await events.GetAsync(id, ct);
        if (item.Kind != "Staff" || item.ResponseDeadline is null) throw ApiError.Missing("Staff event not found.");
        return item;
    }

    private async Task<EventClub> GetInvitationAsync(string eventId, string clubId, CancellationToken ct) =>
        await db.Set<EventClub>().SingleOrDefaultAsync(x => x.EventId == eventId && x.ClubId == clubId, ct)
        ?? throw ApiError.Missing("Invitation not found.");

    private static InvitationDto ToDto(EventClub invite, Event item, DateTime now) =>
        new(invite.EventId, invite.ClubId, invite.Status == "Pending" && item.ResponseDeadline <= now ? "NoResponse" : invite.Status,
            invite.Progress, invite.ExpectedMembers, invite.Note);
}
