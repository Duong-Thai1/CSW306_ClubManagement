using System.ComponentModel.DataAnnotations;

namespace ClubManagement.Dtos;

public sealed record PageFilter([Range(1, 100000)] int Page = 1, [Range(1, 100)] int PageSize = 25)
{
    public int Offset => (Page - 1) * PageSize;
}
public sealed record CreateUserRequest([Required, StringLength(100)] string Username,
    [Required, StringLength(72, MinimumLength = 12)] string Password,
    [Required, StringLength(256)] string FullName, [EmailAddress, StringLength(256)] string? Email,
    [StringLength(100)] string? StudentCode);
public sealed record UpdateUserRequest([Required, StringLength(256)] string FullName,
    [EmailAddress, StringLength(256)] string? Email, [StringLength(100)] string? StudentCode, bool IsActive);
public sealed record UserDto(string Id, string? Username, string FullName, string? Email, string? StudentCode, bool IsActive);
public sealed record SaveClubRequest([Required, StringLength(256)] string Name,
    [Required, StringLength(10000)] string Description, [Required, StringLength(500)] string FieldId,
    bool IsRecruiting, [Range(1, 100000)] int MaxMembers);
public sealed record ClubDto(string Id, string Name, string Description, string FieldId, string Status,
    bool IsRecruiting, int MaxMembers);
public sealed record ClubStatusRequest([Required, RegularExpression("Active|Suspended")] string Status);
public sealed record AssignOwnerRequest([Required, StringLength(500)] string UserId);
public sealed record FieldRequest([Required, StringLength(256)] string Name);
public sealed record FieldDto(string Id, string Name);
public sealed record JoinClubRequest([Required, StringLength(4000)] string Message);
public sealed record ReviewJoinRequest(bool Approve, [StringLength(1000)] string? Reason);
public sealed record JoinRequestDto(string Id, string UserId, string Message, string Status, string? ReviewReason);
public sealed record RecruitmentPostRequest([Required, StringLength(256)] string Title,
    [Required, StringLength(4000)] string Content, DateTimeOffset Deadline);
public sealed record RecruitmentPostDto(string Id, string Title, string Content, DateTime Deadline);
public sealed record MemberProgressDto(string MembershipId, string FullName, string Role, int ContributionPoints,
    int CompletedTasks, int ActiveViolations);
public sealed record ExpelMemberRequest([Required, StringLength(500)] string Reason);
