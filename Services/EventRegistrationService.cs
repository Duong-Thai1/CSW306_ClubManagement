using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class EventRegistrationService(ClubDbContext db, Actor actor, EventAccess access, SqlTransactionLock locks, TimeProvider clock)
{
    public async Task<RegistrationDto> RegisterAsync(string id, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync(EventAccess.LockKey(id), ct);
        var item = await access.GetAsync(id, ct);
        await access.RequireRegistrationEligibilityAsync(item, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (now >= item.StartTime) throw ApiError.Conflict("Registration has closed.");
        var registrations = db.Set<EventRegistration>();
        var entry = await registrations.SingleOrDefaultAsync(x => x.EventId == id && x.UserId == actor.Id, ct);
        if (entry is { CancelledAt: null }) throw ApiError.Conflict("Already registered.");
        if (await registrations.CountAsync(x => x.EventId == id && x.CancelledAt == null, ct) >= item.Capacity)
            throw ApiError.Conflict("The event is full.");

        if (entry is null)
        {
            entry = new EventRegistration { EventId = id, UserId = actor.Id };
            db.Add(entry);
        }
        entry.CancelledAt = null;
        entry.RegisteredAt = now;
        db.Notify(actor.Id, "Registration confirmed: " + item.Title, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(entry.Id, id, now);
    }

    public async Task CancelAsync(string id, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync(EventAccess.LockKey(id), ct);
        var entry = await db.Set<EventRegistration>().SingleOrDefaultAsync(x => x.EventId == id && x.UserId == actor.Id, ct)
            ?? throw ApiError.Missing("Registration not found.");
        if (entry.CancelledAt is null)
        {
            var item = await access.GetAsync(id, ct);
            var now = clock.GetUtcNow().UtcDateTime;
            if (now >= item.StartTime) throw ApiError.Conflict("Cancellation has closed.");
            entry.CancelledAt = now;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<RosterItemDto>> RosterAsync(string id, string? clubId, PageFilter page, CancellationToken ct)
    {
        var item = await access.GetAsync(id, ct);
        var eligible = await access.RosterAsync(item, clubId, ct);
        return await (from user in db.Set<ApplicationUser>()
                      where eligible.Contains(user.Id)
                      orderby user.FullName, user.Id
                      select new RosterItemDto(user.Id, user.FullName, db.Set<Attendance>()
                          .Where(a => a.EventId == id && a.UserId == user.Id).Select(a => (bool?)a.Present).FirstOrDefault(), db.Set<Attendance>()
                          .Where(a => a.EventId == id && a.UserId == user.Id).Select(a => a.Id).FirstOrDefault()))
                      .Skip(page.Offset).Take(page.PageSize).ToListAsync(ct);
    }
}

public sealed class EventAccess(ClubDbContext db, ClubAccess clubAccess, Actor actor)
{
    public static string LockKey(string id) => "event:" + id.ToUpperInvariant();

    public async Task<Event> GetAsync(string id, CancellationToken ct) =>
        await db.Set<Event>().FindAsync([id], ct) ?? throw ApiError.Missing("Event not found.");

    public async Task RequireRegistrationEligibilityAsync(Event item, CancellationToken ct)
    {
        if (item.Kind == "Public" && item.IsPublished && (item.ClubId == null ||
            await db.Set<Club>().AnyAsync(x => x.Id == item.ClubId && x.Status == "Active", ct))) return;

        var memberClubs = from member in db.Set<ClubMember>()
                          join club in db.Set<Club>() on member.ClubId equals club.Id
                          where member.UserId == actor.Id && member.Status == "Active" && club.Status == "Active"
                          select member.ClubId;
        if (item.Kind == "Internal" && item.ClubId is { } owner && await memberClubs.ContainsAsync(owner, ct)) return;
        if (item.Kind == "Staff" && await db.Set<EventClub>().AnyAsync(x => x.EventId == item.Id &&
            x.Status == "Accepted" && memberClubs.Contains(x.ClubId), ct)) return;

        throw ApiError.Forbidden();
    }

    public async Task<IQueryable<string>> RosterAsync(Event item, string? clubId, CancellationToken ct)
    {
        var registrations = ActiveRegistrations(item.Id);
        if (actor.IsStaff) return registrations;

        if (item.Kind != "Staff")
        {
            if (item.ClubId is null) throw ApiError.Forbidden();
            await clubAccess.RequireOwnerAsync(item.ClubId, ct);
            return registrations;
        }
        if (clubId is null) throw ApiError.Bad("Pass clubId for staff events.");
        await clubAccess.RequireOwnerAsync(clubId, ct);
        if (!await db.Set<EventClub>().AnyAsync(x => x.EventId == item.Id && x.ClubId == clubId && x.Status == "Accepted", ct))
            throw ApiError.Forbidden();
        return registrations.Where(id => db.Set<ClubMember>().Any(m => m.ClubId == clubId && m.UserId == id && m.Status == "Active"));
    }

    public IQueryable<string> ActiveRegistrations(string eventId) =>
        db.Set<EventRegistration>().Where(x => x.EventId == eventId && x.CancelledAt == null).Select(x => x.UserId);
}
