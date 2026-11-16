using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class ActivityQueries(ClubDbContext db, Actor actor, ClubAccess access, TimeProvider clock)
{
    public async Task<ActivityPointSummary> PointsAsync(PageFilter page, CancellationToken ct)
    {
        var entries = db.Set<ActivityPointEntry>().AsNoTracking().Where(x => x.UserId == actor.Id);
        var total = await entries.SumAsync(x => (long)x.Points, ct);
        var items = await (from entry in entries join item in db.Set<Event>() on entry.EventId equals item.Id
                           orderby entry.CreatedAt descending, entry.Id
                           select new ActivityPointDto(item.Id, item.Title, entry.Points, entry.CreatedAt.Utc()))
            .Skip(page.Offset).Take(page.PageSize).ToListAsync(ct);
        return new(total, items);
    }

    public async Task<IReadOnlyList<NotificationDto>> NotificationsAsync(PageFilter page, CancellationToken ct) =>
        await VisibleNotifications().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip(page.Offset).Take(page.PageSize)
            .Select(x => new NotificationDto(x.Id, x.ClubId, x.Message, x.CreatedAt.Utc(), x.ReadAt.HasValue ? x.ReadAt.Value.Utc() : null))
            .ToListAsync(ct);

    public async Task ReadNotificationAsync(string id, CancellationToken ct)
    {
        var notification = await VisibleNotifications().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiError.Missing("Notification not found.");
        notification.ReadAt ??= clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task<AnnouncementDto> AnnounceAsync(string clubId, AnnouncementRequest input, CancellationToken ct)
    {
        await access.RequireOwnerAsync(clubId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var announcement = new Announcement
        {
            ClubId = clubId, CreatedByUserId = actor.Id, Title = input.Title, Content = input.Content,
            IsImportant = input.IsImportant, CreatedAt = now
        };
        db.Add(announcement);
        var users = await db.Set<ClubMember>().Where(x => x.ClubId == clubId && x.Status == "Active").Select(x => x.UserId).ToListAsync(ct);
        foreach (var id in users) db.Notify(id, "New club announcement: " + input.Title, now, clubId);
        await db.SaveChangesAsync(ct);
        return new(announcement.Id, announcement.Title, announcement.Content, announcement.IsImportant, now);
    }

    public async Task<IReadOnlyList<AnnouncementDto>> AnnouncementsAsync(string clubId, PageFilter page, CancellationToken ct)
    {
        await access.MemberAsync(clubId, ct);
        return await db.Set<Announcement>().Where(x => x.ClubId == clubId)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(page.Offset).Take(page.PageSize)
            .Select(x => new AnnouncementDto(x.Id, x.Title, x.Content, x.IsImportant, x.CreatedAt.Utc())).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EventReportDto>> ReportAsync(string? clubId, DateTimeOffset from, DateTimeOffset to,
        PageFilter page, CancellationToken ct)
    {
        if (from >= to || from.Year < 1753 || to.Year > 9998) throw ApiError.Bad("Use a valid date range.");
        var events = db.Set<Event>().AsNoTracking().Where(x => x.StartTime >= from.UtcDateTime && x.StartTime < to.UtcDateTime);
        if (clubId is not null)
            events = events.Where(x => x.ClubId == clubId || db.Set<EventClub>().Any(i => i.EventId == x.Id && i.ClubId == clubId));
        return await events.OrderBy(x => x.StartTime).ThenBy(x => x.Id).Skip(page.Offset).Take(page.PageSize)
            .Select(x => new EventReportDto(x.Id, x.ClubId, x.Title, x.StartTime.Utc(),
                db.Set<EventRegistration>().Count(r => r.EventId == x.Id && r.CancelledAt == null &&
                    (clubId == null || x.ClubId == clubId || db.Set<ClubMember>().Any(m => m.ClubId == clubId && m.UserId == r.UserId))),
                db.Set<Attendance>().Count(a => a.EventId == x.Id && a.Present &&
                    (clubId == null || x.ClubId == clubId || db.Set<ClubMember>().Any(m => m.ClubId == clubId && m.UserId == a.UserId))),
                db.Set<EventClub>().Count(i => i.EventId == x.Id && (clubId == null || i.ClubId == clubId)),
                db.Set<EventClub>().Count(i => i.EventId == x.Id && i.Status == "Accepted" && (clubId == null || i.ClubId == clubId))))
            .ToListAsync(ct);
    }

    private IQueryable<Notification> VisibleNotifications()
    {
        var id = actor.Id;
        return db.Set<Notification>().Where(x => x.UserId == id && (x.ClubId == null ||
            db.Set<ClubMember>().Any(m => m.ClubId == x.ClubId && m.UserId == id && m.Status == "Active")));
    }
}
