using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class ClubTaskService(ClubDbContext db, ClubAccess access, Actor actor, SqlTransactionLock locks,
    EventAccess events, ContributionLedger ledger, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<string> CreateTaskAsync(string clubId, CreateTaskRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("points", ct);
        await access.RequireOwnerAsync(clubId, ct);
        var now = Now;
        if (input.Deadline.UtcDateTime <= now || input.Deadline.Year > 9998 ||
            input.RepeatEveryDays.HasValue != input.RepeatUntil.HasValue ||
            (input.RepeatUntil is { } end && (end <= input.Deadline || end > input.Deadline.AddDays(366))))
            throw ApiError.Bad("Choose a future deadline and a valid recurrence range of at most one year.");
        if (input.EventId is { } eventId)
        {
            var item = await events.GetAsync(eventId, ct);
            if (item.ClubId != clubId && !await db.Set<EventClub>().AnyAsync(x => x.EventId == eventId &&
                x.ClubId == clubId && x.Status == "Accepted", ct)) throw ApiError.Forbidden();
        }
        var ids = input.MembershipIds?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        var members = db.Set<ClubMember>().Where(x => x.ClubId == clubId && x.Status == "Active" &&
            db.Set<ApplicationUser>().Any(u => u.Id == x.UserId && u.IsActive));
        if (!input.AllMembers) members = members.Where(x => ids.Contains(x.Id));
        var recipients = await members.ToListAsync(ct);
        if (recipients.Count == 0 || (!input.AllMembers && recipients.Count != ids.Length))
            throw ApiError.Bad("Select active members of this club.");
        var task = new ClubTask
        {
            ClubId = clubId, CreatedByUserId = actor.Id, Title = input.Title, Description = input.Description,
            EventId = input.EventId, Status = "Todo", Priority = input.Priority, Points = input.Points,
            Deadline = input.Deadline.UtcDateTime, CreatedAt = now, FrequencyDays = input.RepeatEveryDays, RepeatUntil = input.RepeatUntil?.UtcDateTime
        };
        db.Add(task);
        Assign(db, task, recipients, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return task.Id;
    }

    public async Task<IReadOnlyList<TaskDto>> MyTasksAsync(PageFilter page, CancellationToken ct)
    {
        var userId = actor.Id;
        return await (from assignment in db.Set<TaskAssignment>()
                      join task in db.Set<ClubTask>() on assignment.ClubTaskId equals task.Id
                      join member in db.Set<ClubMember>() on assignment.MembershipId equals member.Id
                      where member.UserId == userId && member.Status == "Active"
                      orderby task.Deadline, assignment.Id
                      select new TaskDto(assignment.Id, task.Id, task.ClubId, task.Title, task.Description, task.Deadline.Utc(),
                          task.Priority, assignment.Status, task.Points))
            .Skip(page.Offset).Take(page.PageSize).ToListAsync(ct);
    }

    public async Task UpdateTaskAsync(string assignmentId, TaskStatusRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("points", ct);
        var assignment = await db.Set<TaskAssignment>().FindAsync([assignmentId], ct) ?? throw ApiError.Missing("Assignment not found.");
        var task = await db.Set<ClubTask>().FindAsync([assignment.ClubTaskId], ct) ?? throw ApiError.Missing("Task not found.");
        var member = await access.MemberAsync(task.ClubId, ct);
        if (member.Id != assignment.MembershipId) throw ApiError.Forbidden();
        if (assignment.Status == "Done")
        {
            if (input.Status != "Done") throw ApiError.Conflict("Completed tasks cannot be reopened.");
            return;
        }
        assignment.Status = input.Status;
        var now = Now;
        if (input.Status == "Done")
        {
            assignment.CompletedAt = now;
            if (now <= task.Deadline && task.Points > 0) ledger.Add(member, "TaskCompleted", assignment.Id, task.Points, now);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<MemberProgressDto>> ProgressAsync(string clubId, PageFilter page, CancellationToken ct)
    {
        await access.RequireOwnerAsync(clubId, ct);
        return await (from member in db.Set<ClubMember>()
                      join user in db.Set<ApplicationUser>() on member.UserId equals user.Id
                      where member.ClubId == clubId && member.Status == "Active"
                      orderby user.FullName, member.Id
                      select new MemberProgressDto(member.Id, user.FullName, member.Role, member.Points,
                          db.Set<TaskAssignment>().Count(t => t.MembershipId == member.Id && t.Status == "Done"),
                          db.Set<Violation>().Count(v => v.ClubMemberId == member.Id && v.Status != "Waived")))
            .Skip(page.Offset).Take(page.PageSize).ToListAsync(ct);
    }

    internal static void Assign(ClubDbContext db, ClubTask task, IEnumerable<ClubMember> recipients, DateTime now)
    {
        foreach (var member in recipients)
        {
            db.Add(new TaskAssignment { ClubTaskId = task.Id, MembershipId = member.Id, Status = "Todo" });
            db.Notify(member.UserId, "New club task: " + task.Title, now, task.ClubId);
        }
    }
}
