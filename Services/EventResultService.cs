using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class EventResultService(ClubDbContext db, EventAccess access, Actor actor, SqlTransactionLock locks,
    TimeProvider clock)
{
    public async Task RecordAttendanceAsync(string id, string? clubId, RecordAttendanceRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("points", ct);
        await locks.LockWithinTransactionAsync(EventAccess.LockKey(id), ct);
        var item = await access.GetAsync(id, ct);
        var roster = await access.RosterAsync(item, clubId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        if (now < item.StartTime ||
            TimeZoneInfo.ConvertTimeFromUtc(now, zone).Date != TimeZoneInfo.ConvertTimeFromUtc(item.StartTime.Utc(), zone).Date)
            throw ApiError.Conflict("Attendance can be recorded on the event day after the start.");
        if (await db.Set<EventResult>().AnyAsync(x => x.EventId == id, ct)) throw ApiError.Conflict("Results are final.");
        if (input.Entries.Any(x => x is null)) throw ApiError.Bad("Attendance entries cannot be null.");
        var ids = input.Entries.Select(x => x.UserId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (ids.Length != input.Entries.Length || await roster.CountAsync(x => ids.Contains(x), ct) != ids.Length)
            throw ApiError.Bad("Attendance must contain unique registered students in your roster.");
        var existing = await db.Set<Attendance>().Where(x => x.EventId == id && ids.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var inputEntry in input.Entries)
        {
            if (!existing.TryGetValue(inputEntry.UserId, out var entry))
            {
                entry = new Attendance { EventId = id, UserId = inputEntry.UserId };
                db.Add(entry);
            }
            else if (entry.Present != inputEntry.Present && await db.Set<Violation>().AnyAsync(x => x.AttendanceId == entry.Id, ct))
            {
                throw ApiError.Conflict("Resolve the attendance penalty before changing the attendance.");
            }
            entry.Present = inputEntry.Present;
            entry.RecordedAt = now;
            entry.RecordedByUserId = actor.Id;
            db.Audit(actor.Id, "AttendanceRecorded", entry.Id, now);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<ConfirmResultDto> ConfirmAsync(string id, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("points", ct);
        await locks.LockWithinTransactionAsync(EventAccess.LockKey(id), ct);
        var item = await access.GetAsync(id, ct);
        if (item.Kind != "Public" || !item.IsPublished) throw ApiError.Conflict("Only published public events award activity points.");
        var result = await db.Set<EventResult>().FindAsync([id], ct);
        if (result is not null)
        {
            var count = await db.Set<ActivityPointEntry>().CountAsync(x => x.EventId == id, ct);
            return new(id, count, result.ConfirmedAt.Utc());
        }
        var now = clock.GetUtcNow().UtcDateTime;
        if (now < (item.EndTime ?? item.StartTime)) throw ApiError.Conflict("Wait until the event finishes.");
        var registrations = await access.ActiveRegistrations(id).ToListAsync(ct);
        var attendance = await db.Set<Attendance>().Where(x => x.EventId == id).ToListAsync(ct);
        var recorded = attendance.Select(x => x.UserId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (registrations.Any(x => !recorded.Contains(x)))
            throw ApiError.Conflict("Record attendance for every registered student before confirming.");
        var present = attendance.Where(x => x.Present).Select(x => x.UserId)
            .Intersect(registrations, StringComparer.OrdinalIgnoreCase).ToArray();

        db.Add(new EventResult { EventId = id, ConfirmedAt = now, ConfirmedByUserId = actor.Id });
        foreach (var userId in present)
        {
            db.Add(new ActivityPointEntry { EventId = id, UserId = userId, Points = item.ActivityPoints, CreatedAt = now });
            db.Notify(userId, $"Activity points awarded: {item.ActivityPoints} for {item.Title}.", now);
        }
        db.Audit(actor.Id, "EventResultConfirmed", id, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(id, present.Length, now);
    }
}
