using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class EventProposalService(ClubDbContext db, ClubAccess access, Actor actor, SqlTransactionLock locks, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<ProposalDto> SaveAsync(string clubId, string? id, SaveProposalRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("proposals", ct);
        await access.RequireOwnerAsync(clubId, ct);
        EventRules.ValidateDates(input.StartTime, input.EndTime, Now);
        var proposal = id is null ? new EventProposal { ClubId = clubId, CreatedByUserId = actor.Id, Status = "Draft" }
            : await GetAsync(id, ct);
        if (proposal.ClubId != clubId) throw ApiError.Missing("Proposal not found.");
        if (proposal.Status is not ("Draft" or "ChangesRequested")) throw ApiError.Conflict("This proposal cannot be edited.");
        (proposal.Title, proposal.Objective, proposal.Description, proposal.Venue, proposal.StartTime, proposal.EndTime, proposal.Capacity) =
            (input.Title, input.Objective, input.Description, input.Venue, input.StartTime.UtcDateTime, input.EndTime.UtcDateTime, input.Capacity);
        if (id is null) db.Add(proposal);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(proposal, await ReviewsAsync(proposal.Id, ct));
    }

    public async Task SubmitAsync(string id, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("proposals", ct);
        var proposal = await GetAsync(id, ct);
        await access.RequireOwnerAsync(proposal.ClubId, ct);
        if (proposal.Status is not ("Draft" or "ChangesRequested") || proposal.StartTime <= Now)
            throw ApiError.Conflict("This proposal cannot be submitted.");
        proposal.Status = "Pending";
        db.Audit(actor.Id, "ProposalSubmitted", id, Now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<string?> ReviewAsync(string id, ReviewProposalRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("proposals", ct);
        var proposal = await GetAsync(id, ct);
        if (proposal.Status != "Pending") throw ApiError.Conflict("Only a pending proposal can be reviewed.");
        var now = Now;
        string? eventId = null;
        if (input.Decision == "Approved")
        {
            if (input.ActivityPoints is null) throw ApiError.Bad("Approval requires activity points.");
            if (proposal.StartTime <= now) throw ApiError.Conflict("The proposed start time has passed.");
            if (!await db.Set<Club>().AnyAsync(x => x.Id == proposal.ClubId && x.Status == "Active", ct))
                throw ApiError.Conflict("The club is inactive.");
            var published = new Event
            {
                ClubId = proposal.ClubId, ProposalId = id, CreatedByUserId = proposal.CreatedByUserId,
                Title = proposal.Title, Description = proposal.Description, Venue = proposal.Venue, Capacity = proposal.Capacity,
                Kind = "Public", IsPublished = true, ActivityPoints = input.ActivityPoints.Value,
                StartTime = proposal.StartTime, EndTime = proposal.EndTime, CreatedAt = now
            };
            eventId = published.Id;
            db.Add(published);
            var students = await db.Set<ApplicationUser>().Where(x => x.SystemRole == "Student" && x.IsActive)
                .Select(x => x.Id).ToListAsync(ct);
            foreach (var studentId in students) db.Notify(studentId, "New event: " + published.Title, now);
        }
        else if (string.IsNullOrWhiteSpace(input.Feedback))
        {
            throw ApiError.Bad("Feedback is required.");
        }

        proposal.Status = input.Decision;
        db.Add(new ProposalReview { ProposalId = id, ActorId = actor.Id, Decision = input.Decision, Feedback = input.Feedback, CreatedAt = now });
        db.Notify(proposal.CreatedByUserId, "Proposal decision: " + input.Decision, now, proposal.ClubId);
        db.Audit(actor.Id, "Proposal:" + input.Decision, id, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return eventId;
    }

    public async Task<ProposalDto> DetailAsync(string id, CancellationToken ct)
    {
        var proposal = await GetAsync(id, ct);
        if (!actor.IsStaff) await access.RequireOwnerAsync(proposal.ClubId, ct);
        return ToDto(proposal, await ReviewsAsync(id, ct));
    }

    public async Task<IReadOnlyList<ProposalDto>> ListAsync(string? clubId, bool pending, PageFilter page, CancellationToken ct)
    {
        if (!actor.IsStaff)
        {
            if (clubId is null) throw ApiError.Forbidden();
            await access.RequireOwnerAsync(clubId, ct);
        }
        var query = db.Set<EventProposal>().Where(x => clubId == null || x.ClubId == clubId);
        if (pending) query = query.Where(x => x.Status == "Pending");
        var proposals = await query.OrderByDescending(x => x.StartTime).ThenBy(x => x.Id)
            .Skip(page.Offset).Take(page.PageSize).ToListAsync(ct);
        return proposals.Select(x => ToDto(x, [])).ToArray();
    }

    private async Task<EventProposal> GetAsync(string id, CancellationToken ct) =>
        await db.Set<EventProposal>().FindAsync([id], ct) ?? throw ApiError.Missing("Proposal not found.");

    private async Task<IReadOnlyList<ProposalReviewDto>> ReviewsAsync(string id, CancellationToken ct) =>
        await db.Set<ProposalReview>().Where(x => x.ProposalId == id).OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProposalReviewDto(x.Decision, x.Feedback, x.CreatedAt.Utc())).ToListAsync(ct);

    private static ProposalDto ToDto(EventProposal x, IReadOnlyList<ProposalReviewDto> reviews) =>
        new(x.Id, x.ClubId, x.Title, x.Objective, x.Description, x.Venue, x.StartTime.Utc(), x.EndTime.Utc(), x.Capacity, x.Status, reviews);
}

internal static class EventRules
{
    public static void ValidateDates(DateTimeOffset start, DateTimeOffset end, DateTime now)
    {
        if (start.UtcDateTime <= now || end <= start || end.Year > 9998)
            throw ApiError.Bad("Start time must be in the future and end time must follow it.");
    }
}
