using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubManagement.Services;

public sealed class UserAdminService(ClubDbContext db, Actor actor, TimeProvider clock, SqlTransactionLock locks)
{
    public async Task<UserDto> CreateAsync(CreateUserRequest input, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            Username = AuthService.NormalizeUsername(input.Username), PasswordHash = AuthService.HashPassword(input.Password),
            FullName = input.FullName, Email = input.Email, StudentCode = input.StudentCode,
            IsActive = true, SystemRole = "Student", CreatedAt = now
        };
        db.Add(user);
        db.Audit(actor.Id, "StudentCreated", user.Id, now);
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(string userId, UpdateUserRequest input, CancellationToken ct)
    {
        await using var transaction = await locks.AcquireAsync("memberships", ct);
        await locks.LockWithinTransactionAsync(AuthService.AccountLock(userId), ct);
        var user = await db.Set<ApplicationUser>().SingleOrDefaultAsync(x => x.Id == userId && x.SystemRole == "Student", ct)
            ?? throw ApiError.Missing("Student account not found.");
        if (!input.IsActive && await db.Set<ClubMember>().AnyAsync(x => x.UserId == userId && x.Role == "Owner" && x.Status == "Active", ct))
            throw ApiError.Conflict("Transfer club ownership before disabling this account.");
        (user.FullName, user.Email, user.StudentCode, user.IsActive) =
            (input.FullName, input.Email, input.StudentCode, input.IsActive);
        var now = clock.GetUtcNow().UtcDateTime;
        if (!input.IsActive)
            await db.Set<UserSession>().Where(x => x.UserId == userId && x.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, (DateTime?)now), ct);
        db.Audit(actor.Id, "StudentUpdated", userId, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(user);
    }

    private static UserDto ToDto(ApplicationUser user) =>
        new(user.Id, user.Username, user.FullName, user.Email, user.StudentCode, user.IsActive);
}
