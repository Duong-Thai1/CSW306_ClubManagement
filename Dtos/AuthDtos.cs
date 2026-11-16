using System.ComponentModel.DataAnnotations;

namespace ClubManagement.Dtos;

public sealed record LoginRequest([Required, StringLength(100)] string Username,
    [Required, StringLength(72)] string Password);
public sealed record RefreshRequest([Required, StringLength(128)] string RefreshToken);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, DateTime RefreshExpiresAt);
