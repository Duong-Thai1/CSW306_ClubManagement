using ClubManagement.Data;
using ClubManagement.Models;

namespace ClubManagement.Services;

internal static class Records
{
    public static void Notify(this ClubDbContext db, string userId, string message, DateTime at, string? clubId = null)
        => db.Add(new Notification { UserId = userId, Message = message, CreatedAt = at, ClubId = clubId });

    public static void Audit(this ClubDbContext db, string actorId, string action, string targetId, DateTime at)
        => db.Add(new AuditEntry { ActorId = actorId, Action = action, TargetId = targetId, CreatedAt = at });

    public static DateTime Utc(this DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
