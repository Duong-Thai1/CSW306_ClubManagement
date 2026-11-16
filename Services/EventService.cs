using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class EventService(ClubDbContext db, ClubAccess access, Actor actor, TimeProvider clock)
{
    public async Task<IReadOnlyList<EventDto>> ListAsync(EventFilter filter, CancellationToken ct)
    {
        var query = VisibleEvents();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x => x.Title.Contains(search) || x.Description.Contains(search));
        }
        if (filter.ClubId is not null) query = query.Where(x => x.ClubId == filter.ClubId);
        if (filter.From is { } from) query = query.Where(x => x.StartTime >= from.UtcDateTime);
        if (filter.To is { } to) query = query.Where(x => x.StartTime < to.UtcDateTime);
        if (filter.From >= filter.To && filter.To.HasValue) throw ApiError.Bad("From must precede To.");
        return await Project(query.OrderBy(x => x.StartTime).ThenBy(x => x.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)).ToListAsync(ct);
    }

    public async Task<EventDto> DetailAsync(string id, CancellationToken ct) =>
        await Project(VisibleEvents().Where(x => x.Id == id)).SingleOrDefaultAsync(ct)
        ?? throw ApiError.Missing("Event not found.");

    public async Task<EventDto> CreateAsync(string? clubId, CreateEventRequest input, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        EventRules.ValidateDates(input.StartTime, input.EndTime, now);
        if (clubId is not null) await access.RequireOwnerAsync(clubId, ct);
        if (clubId is null && (input.ResponseDeadline is null || input.ResponseDeadline.Value.UtcDateTime <= now ||
            input.ResponseDeadline.Value >= input.StartTime))
            throw ApiError.Bad("Staff events require a response deadline between now and the event start.");
        var fields = input.FieldIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray() ?? [];
        if (fields.Length != await db.Set<ClubField>().CountAsync(x => fields.Contains(x.Id), ct))
            throw ApiError.Bad("At least one event field does not exist.");

        var item = new Event
        {
            ClubId = clubId, CreatedByUserId = actor.Id, Kind = clubId is null ? "Staff" : "Internal",
            Title = input.Title, Description = input.Description, Venue = input.Venue, Capacity = input.Capacity,
            StartTime = input.StartTime.UtcDateTime, EndTime = input.EndTime.UtcDateTime,
            IsMandatory = input.IsMandatory, CreatedAt = now, ResponseDeadline = clubId is null ? input.ResponseDeadline?.UtcDateTime : null,
            RequiredClubCount = input.RequiredClubCount, MinimumMembersPerClub = input.MinimumMembersPerClub
        };
        db.Add(item);
        foreach (var field in fields) db.Add(new EventClubFieldRelation { EventId = item.Id, Field = field });
        db.Audit(actor.Id, "EventCreated", item.Id, now);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(item.Id, ct);
    }

    private IQueryable<Event> VisibleEvents()
    {
        var id = actor.Id;
        var staff = actor.IsStaff;
        var activeClubs = db.Set<Club>().Where(x => x.Status == "Active").Select(x => x.Id);
        var memberships = db.Set<ClubMember>().Where(x => x.UserId == id && x.Status == "Active" && activeClubs.Contains(x.ClubId));
        return db.Set<Event>().AsNoTracking().Where(x => staff ||
            (x.IsPublished && x.Kind == "Public" && (x.ClubId == null || activeClubs.Contains(x.ClubId))) ||
            (x.Kind == "Internal" && memberships.Any(m => m.ClubId == x.ClubId)) ||
            (x.Kind == "Staff" && db.Set<EventClub>().Any(i => i.EventId == x.Id &&
                memberships.Any(m => m.ClubId == i.ClubId && (i.Status == "Accepted" || m.Role == "Owner")))));
    }

    private IQueryable<EventDto> Project(IQueryable<Event> query) => query.Select(x => new EventDto(x.Id, x.Title, x.ClubId,
        db.Set<Club>().Where(c => c.Id == x.ClubId).Select(c => c.Name).FirstOrDefault(),
        x.Description, x.Venue, x.StartTime.Utc(), x.EndTime.HasValue ? x.EndTime.Value.Utc() : null,
        x.Capacity, x.Capacity - db.Set<EventRegistration>().Count(r => r.EventId == x.Id && r.CancelledAt == null),
        x.ActivityPoints, x.Kind, x.IsPublished));
}
