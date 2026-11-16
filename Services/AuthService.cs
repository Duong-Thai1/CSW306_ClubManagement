using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClubManagement.Data;
using ClubManagement.Dtos;
using ClubManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ClubManagement.Services;

public sealed class JwtSettings
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string SigningKey { get; init; } = "";
    public int AccessTokenDays { get; init; } = 3;
    public int RefreshGraceHours { get; init; } = 5;
    public SymmetricSecurityKey SecurityKey => new(Encoding.UTF8.GetBytes(SigningKey));
}

public sealed class AuthService(ClubDbContext db, SqlTransactionLock locks, JwtSettings settings, TimeProvider clock)
{
    public async Task<TokenResponse> LoginAsync(LoginRequest input, CancellationToken ct)
    {
        var username = NormalizeUsername(input.Username);
        if (Encoding.UTF8.GetByteCount(input.Password) > 72) throw ApiError.Unauthorized();
        var user = await db.Set<ApplicationUser>().AsNoTracking().SingleOrDefaultAsync(x => x.Username == username, ct);
        if (user is null || !user.IsActive || user.PasswordHash is null ||
            !BCrypt.Net.BCrypt.Verify(input.Password, user.PasswordHash))
            throw ApiError.Unauthorized();

        await using var transaction = await locks.AcquireAsync(AccountLock(user.Id), ct);
        if (!await db.Set<ApplicationUser>().AnyAsync(x => x.Id == user.Id && x.IsActive, ct)) throw ApiError.Unauthorized();
        var response = Issue(user, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest input, CancellationToken ct)
    {
        var hash = HashRefreshToken(input.RefreshToken);
        var userId = await db.Set<UserSession>().Where(x => x.RefreshHash == hash).Select(x => x.UserId)
            .SingleOrDefaultAsync(ct) ?? throw ApiError.Unauthorized();
        await using var transaction = await locks.AcquireAsync(AccountLock(userId), ct);
        var session = await db.Set<UserSession>().SingleOrDefaultAsync(x => x.RefreshHash == hash, ct);
        var now = clock.GetUtcNow();
        if (session is null || session.RevokedAt != null || now.UtcDateTime >= session.RefreshExpiresAt)
            throw ApiError.Unauthorized();
        if (now.UtcDateTime < session.AccessExpiresAt)
            throw ApiError.Conflict("Refresh becomes available when the access token expires.");

        var user = await db.Set<ApplicationUser>().SingleOrDefaultAsync(x => x.Id == session.UserId && x.IsActive, ct)
            ?? throw ApiError.Unauthorized();
        session.RevokedAt = now.UtcDateTime;
        var response = Issue(user, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return response;
    }

    public async Task LogoutAsync(string sessionId, string userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.Set<UserSession>().Where(x => x.Id == sessionId && x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAt, (DateTime?)now), ct);
    }

    public async Task BootstrapAsync(string? username, string? password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 100 || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Set Bootstrap:Username and Bootstrap:Password.");
        await using var transaction = await locks.AcquireAsync("bootstrap-staff", ct);
        if (await db.Set<ApplicationUser>().AnyAsync(x => x.SystemRole == "Staff", ct))
            throw new InvalidOperationException("A staff account already exists.");
        db.Add(new ApplicationUser
        {
            Username = NormalizeUsername(username), PasswordHash = HashPassword(password), FullName = "CTSV",
            SystemRole = "Staff", IsActive = true, CreatedAt = clock.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public static string NormalizeUsername(string value) => value.Trim().ToUpperInvariant();

    public static string HashPassword(string password)
    {
        if (password.Length < 12 || Encoding.UTF8.GetByteCount(password) > 72)
            throw ApiError.Bad("Password must have at least 12 characters and at most 72 UTF-8 bytes.");
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    private TokenResponse Issue(ApplicationUser user, DateTimeOffset now)
    {
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds()).UtcDateTime;
        var expiresAt = issuedAt.AddDays(settings.AccessTokenDays);
        var refreshExpiresAt = expiresAt.AddHours(settings.RefreshGraceHours);
        var refreshToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        var session = new UserSession
        {
            UserId = user.Id, RefreshHash = HashRefreshToken(refreshToken),
            AccessExpiresAt = expiresAt, RefreshExpiresAt = refreshExpiresAt
        };
        db.Add(session);
        var claims = new[]
        {
            new Claim("sub", user.Id), new Claim("sid", session.Id),
            new Claim("role", user.SystemRole), new Claim("jti", Guid.NewGuid().ToString("N"))
        };
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, issuedAt, expiresAt,
            new SigningCredentials(settings.SecurityKey, SecurityAlgorithms.HmacSha256));
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), refreshToken, expiresAt, refreshExpiresAt);
    }

    internal static string AccountLock(string userId) => "account:" + userId.ToUpperInvariant();

    private static string HashRefreshToken(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
