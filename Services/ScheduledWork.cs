using ClubManagement.Data;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class ScheduledWork(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ScheduledWork> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ClubDbContext>();
                var locks = scope.ServiceProvider.GetRequiredService<SqlTransactionLock>();
                await RunOnceAsync(db, locks, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning(error, "Scheduled work did not complete."); }
        }
    }

    private async Task RunOnceAsync(ClubDbContext db, SqlTransactionLock locks, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expiredEvents = await db.Set<EventClub>().Where(x => x.Status == "Pending" &&
            db.Set<Event>().Any(e => e.Id == x.EventId && e.ResponseDeadline <= now))
            .Select(x => x.EventId).Distinct().ToListAsync(ct);
        foreach (var eventId in expiredEvents)
        {
            await using var expiration = await locks.AcquireAsync(EventAccess.LockKey(eventId), ct);
            await db.Set<EventClub>().Where(x => x.EventId == eventId && x.Status == "Pending")
                .ExecuteUpdateAsync(change => change.SetProperty(x => x.Status, "NoResponse").SetProperty(x => x.UpdatedAt, now), ct);
            await expiration.CommitAsync(ct);
        }

        await using var transaction = await locks.AcquireAsync("points", ct);
        var templates = await db.Set<ClubTask>().Where(x => x.FrequencyDays != null && x.RepeatUntil != null &&
            x.Deadline <= now && db.Set<Club>().Any(c => c.Id == x.ClubId && c.Status == "Active")).ToListAsync(ct);

        foreach (var template in templates)
        {
            var interval = template.FrequencyDays!.Value;
            var end = template.RepeatUntil!.Value;
            var previousDeadline = await db.Set<ClubTask>().Where(x => x.TemplateId == template.Id)
                .MaxAsync(x => (DateTime?)x.Deadline, ct) ?? template.Deadline;
            var next = previousDeadline.AddDays(interval);
            if (next > end || previousDeadline > now) continue;

            var members = await (from membership in db.Set<ClubMember>()
                                 join assignment in db.Set<TaskAssignment>() on membership.Id equals assignment.MembershipId
                                 join user in db.Set<ApplicationUser>() on membership.UserId equals user.Id
                                 where assignment.ClubTaskId == template.Id && membership.Status == "Active" && user.IsActive
                                 select membership).ToListAsync(ct);

            var missing = 0;
            while (next <= end && next.AddDays(-interval) <= now && missing < 30)
            {
                var task = new ClubTask
                {
                    TemplateId = template.Id, OccursAt = next.AddDays(-interval), ClubId = template.ClubId,
                    EventId = template.EventId, Title = template.Title, Description = template.Description,
                    Deadline = next, Priority = template.Priority, Points = template.Points, Status = "Todo",
                    CreatedByUserId = template.CreatedByUserId, CreatedAt = now
                };
                db.Add(task);
                ClubTaskService.Assign(db, task, members, now);
                next = next.AddDays(interval);
                missing++;
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
