using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class ClubDisciplineService(ClubDbContext db, ClubAccess access, Actor actor, SqlTransactionLock locks,
    EventAccess events, ContributionLedger ledger, TimeProvider clock, IConfiguration config)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<AbsenceDto> SubmitAbsenceAsync(string clubId, SubmitAbsenceRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("points", ct);
        var member = await access.MemberAsync(clubId, ct);
        if ((input.EventId is null) == (input.TaskAssignmentId is null)) throw ApiError.Bad("Choose one event or task assignment.");
        var now = Now;
        var isLate = false;
        if (input.EventId is { } eventId)
        {
            var item = await events.GetAsync(eventId, ct);
            await RequireMemberEventAsync(member, item, ct);
            isLate = now >= item.StartTime;
            var deadline = (item.EndTime ?? item.StartTime).AddHours(48);
            if (now > deadline) throw ApiError.Conflict("The absence request window has closed.");
        }
        else
        {
            var (_, task) = await LoadAssignmentAsync(input.TaskAssignmentId!, member, ct);
            if (now >= task.Deadline) throw ApiError.Conflict("The task absence deadline has passed.");
        }
        var request = new AbsenceRequest
        {
            MembershipId = member.Id, EventId = input.EventId, TaskAssignmentId = input.TaskAssignmentId,
            Status = "Pending", Reason = input.Reason, EvidenceReference = input.EvidenceReference, CreatedAt = now, IsLate = isLate
        };
        db.Add(request);
        var owner = await db.Set<ClubMember>().Where(x => x.ClubId == clubId && x.Role == "Owner" && x.Status == "Active")
            .Select(x => x.UserId).SingleOrDefaultAsync(ct);
        if (owner is not null) db.Notify(owner, "New absence request.", now, clubId);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(request);
    }

    public async Task<IReadOnlyList<AbsenceDto>> AbsencesAsync(string clubId, PageFilter page, CancellationToken ct)
    {
        var actorMember = await access.MemberAsync(clubId, ct);
        return await (from absence in db.Set<AbsenceRequest>()
                      join member in db.Set<ClubMember>() on absence.MembershipId equals member.Id
                      where member.ClubId == clubId && (member.Id == actorMember.Id || actorMember.Role == "Owner")
                      orderby absence.CreatedAt descending, absence.Id
                      select new AbsenceDto(absence.Id, absence.MembershipId, absence.EventId, absence.TaskAssignmentId,
                          absence.Status, absence.IsLate, absence.Reason, absence.EvidenceReference))
            .Skip(page.Offset).Take(page.PageSize).ToListAsync(ct);
    }

    public async Task<AbsenceDto> ReviewAbsenceAsync(string clubId, string id, bool approve, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("points", ct);
        await access.RequireOwnerAsync(clubId, ct);
        var request = await db.Set<AbsenceRequest>().FindAsync([id], ct) ?? throw ApiError.Missing("Absence request not found.");
        var member = await LoadMemberAsync(clubId, request.MembershipId, ct);
        if (request.Status != "Pending") throw ApiError.Conflict("This request was already reviewed.");
        var now = Now;
        request.Status = approve ? "Approved" : "Rejected";
        request.ReviewedAt = now;
        request.ReviewedByUserId = actor.Id;
        if (approve)
        {
            var penalties = db.Set<Violation>().Where(v => v.ClubMemberId == member.Id && v.Status != "Waived");
            var penalty = request.EventId is { } eventId
                ? await penalties.SingleOrDefaultAsync(v => db.Set<Attendance>().Any(a => a.Id == v.AttendanceId && a.EventId == eventId), ct)
                : await penalties.SingleOrDefaultAsync(v => db.Set<TaskAssignment>().Any(a =>
                    a.Id == request.TaskAssignmentId && a.ClubTaskId == v.ClubTaskId), ct);
            if (penalty is not null) Waive(member, penalty, "Absence request approved.", now);
        }
        db.Notify(member.UserId, "Absence request: " + request.Status, now, clubId);
        db.Audit(actor.Id, "Absence:" + request.Status, request.Id, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(request);
    }

    public async Task<PenaltyDto> PenalizeAsync(string clubId, string memberId, CreatePenaltyRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("points", ct);
        await access.RequireOwnerAsync(clubId, ct);
        var member = await LoadMemberAsync(clubId, memberId, ct);
        if ((input.AttendanceId is null) == (input.TaskAssignmentId is null)) throw ApiError.Bad("Choose one attendance or assignment.");
        var now = Now;
        string? taskId = null;
        int deduction;
        if (input.AttendanceId is { } attendanceId)
        {
            var attendance = await db.Set<Attendance>().FindAsync([attendanceId], ct) ?? throw ApiError.Missing("Attendance not found.");
            if (attendance.UserId != member.UserId) throw ApiError.Forbidden();
            var item = await events.GetAsync(attendance.EventId, ct);
            await RequireMemberEventAsync(member, item, ct);
            if (attendance.Present || !item.IsMandatory || now < (item.EndTime ?? item.StartTime))
                throw ApiError.Conflict("This attendance does not qualify for a penalty.");
            if (await db.Set<AbsenceRequest>().AnyAsync(x => x.MembershipId == memberId && x.EventId == item.Id &&
                x.Status == "Approved", ct)) throw ApiError.Conflict("An approved absence exists.");
            deduction = config.GetValue("ClubRules:MandatoryAbsenceDeduction", 5);
        }
        else
        {
            var (assignment, task) = await LoadAssignmentAsync(input.TaskAssignmentId!, member, ct);
            if (now <= task.Deadline || assignment.CompletedAt <= task.Deadline ||
                await db.Set<AbsenceRequest>().AnyAsync(x => x.TaskAssignmentId == assignment.Id && x.Status == "Approved", ct))
                throw ApiError.Conflict("This task does not qualify for a penalty.");
            taskId = task.Id;
            deduction = config.GetValue("ClubRules:OverdueTaskDeduction", 2);
        }
        var penalty = new Violation
        {
            ClubMemberId = member.Id, RecordedByUserId = actor.Id, Reason = input.Reason,
            AttendanceId = input.AttendanceId, ClubTaskId = taskId,
            PointsDeducted = deduction, Status = "Active", CreatedAt = now
        };
        db.Add(penalty);
        ledger.Add(member, "Penalty", penalty.Id, -deduction, now);
        db.Notify(member.UserId, $"Club penalty: {deduction} contribution points deducted.", now, clubId);
        db.Audit(actor.Id, "PenaltyRecorded", penalty.Id, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(penalty);
    }

    public async Task<PenaltyDto> AppealAsync(string clubId, string id, AppealRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("points", ct);
        var member = await access.MemberAsync(clubId, ct);
        var penalty = await GetPenaltyAsync(id, ct);
        if (penalty.ClubMemberId != member.Id) throw ApiError.Forbidden();
        var now = Now;
        if (penalty.Status != "Active" || penalty.AppealedAt.HasValue || now > penalty.CreatedAt.AddDays(3))
            throw ApiError.Conflict("The appeal is no longer available.");
        penalty.Status = "Appealed";
        penalty.AppealReason = input.Reason;
        penalty.AppealedAt = now;
        db.Audit(actor.Id, "PenaltyAppealed", penalty.Id, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(penalty);
    }

    public async Task<PenaltyDto> ResolveAsync(string clubId, string id, PenaltyDecisionRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("points", ct);
        await access.RequireOwnerAsync(clubId, ct);
        var penalty = await GetPenaltyAsync(id, ct);
        var member = await LoadMemberAsync(clubId, penalty.ClubMemberId, ct);
        var now = Now;
        if (penalty.Status == "Waived" || (!input.Waive && penalty.Status != "Appealed"))
            throw ApiError.Conflict("This penalty cannot be changed.");
        if (input.Waive) Waive(member, penalty, input.Reason, now);
        else
        {
            penalty.Status = "Active";
            penalty.ResolutionReason = input.Reason;
            db.Audit(actor.Id, "AppealDenied", penalty.Id, now);
        }
        db.Notify(member.UserId, "Penalty decision: " + penalty.Status, now, clubId);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(penalty);
    }

    public async Task<ContributionDto> ContributionAsync(string clubId, string? memberId, PageFilter page, CancellationToken ct)
    {
        ClubMember member;
        if (memberId is null) member = await access.MemberAsync(clubId, ct);
        else
        {
            if (!actor.IsStaff) await access.RequireOwnerAsync(clubId, ct);
            member = await LoadMemberAsync(clubId, memberId, ct);
        }
        var query = db.Set<Violation>().Where(x => x.ClubMemberId == member.Id);
        var now = Now;
        var semesterStart = new DateTime(now.Year, now.Month >= 7 ? 7 : 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var violations = await query.CountAsync(x => x.Status != "Waived" && x.CreatedAt >= semesterStart, ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(page.Offset).Take(page.PageSize).ToListAsync(ct);
        return new(member.Points, violations, violations >= config.GetValue("ClubRules:WarningThreshold", 3),
            violations >= config.GetValue("ClubRules:ExpulsionReviewThreshold", 5), items.Select(ToDto).ToArray());
    }

    private async Task RequireMemberEventAsync(ClubMember member, Event item, CancellationToken ct)
    {
        if (item.ClubId != member.ClubId && !await db.Set<EventClub>().AnyAsync(x => x.EventId == item.Id &&
            x.ClubId == member.ClubId && x.Status == "Accepted", ct)) throw ApiError.Forbidden();
        if (!await events.ActiveRegistrations(item.Id).ContainsAsync(member.UserId, ct))
            throw ApiError.Conflict("The student is not registered for this event.");
    }

    private async Task<(TaskAssignment Assignment, ClubTask Task)> LoadAssignmentAsync(string id, ClubMember member, CancellationToken ct)
    {
        var assignment = await db.Set<TaskAssignment>().SingleOrDefaultAsync(x => x.Id == id && x.MembershipId == member.Id, ct)
            ?? throw ApiError.Missing("Assignment not found.");
        var task = await db.Set<ClubTask>().FindAsync([assignment.ClubTaskId], ct) ?? throw ApiError.Missing("Task not found.");
        if (task.ClubId != member.ClubId) throw ApiError.Forbidden();
        return (assignment, task);
    }

    private async Task<ClubMember> LoadMemberAsync(string clubId, string id, CancellationToken ct) =>
        await db.Set<ClubMember>().SingleOrDefaultAsync(x => x.Id == id && x.ClubId == clubId, ct)
            ?? throw ApiError.Missing("Membership not found.");

    private async Task<Violation> GetPenaltyAsync(string id, CancellationToken ct) =>
        await db.Set<Violation>().FindAsync([id], ct) ?? throw ApiError.Missing("Penalty not found.");

    private void Waive(ClubMember member, Violation penalty, string reason, DateTime now)
    {
        penalty.Status = "Waived";
        penalty.ResolutionReason = reason;
        ledger.Add(member, "PenaltyRefund", penalty.Id, penalty.PointsDeducted, now);
        db.Audit(actor.Id, "PenaltyWaived", penalty.Id, now);
    }

    private static AbsenceDto ToDto(AbsenceRequest x) => new(x.Id, x.MembershipId, x.EventId, x.TaskAssignmentId,
        x.Status, x.IsLate, x.Reason, x.EvidenceReference);
    private static PenaltyDto ToDto(Violation x) => new(x.Id, x.Status, x.Reason, x.PointsDeducted,
        x.CreatedAt.Utc(), x.ResolutionReason, x.AppealReason);
}
