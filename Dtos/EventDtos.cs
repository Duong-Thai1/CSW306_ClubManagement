using System.ComponentModel.DataAnnotations;

namespace ClubManagement.Dtos;

public sealed record EventFilter(string? Search, string? ClubId, DateTimeOffset? From, DateTimeOffset? To,
    [Range(1, 100000)] int Page = 1, [Range(1, 100)] int PageSize = 25);
public sealed record EventDto(string Id, string Title, string? ClubId, string? ClubName, string Description, string Venue,
    DateTime StartTime, DateTime? EndTime, int Capacity, int SeatsLeft, int ActivityPoints, string Kind, bool IsPublished);
public sealed record SaveProposalRequest([Required, StringLength(256)] string Title,
    [Required, StringLength(1000)] string Objective, [Required, StringLength(10000)] string Description,
    [Required, StringLength(256)] string Venue, DateTimeOffset StartTime, DateTimeOffset EndTime, [Range(1, 100000)] int Capacity);
public sealed record ReviewProposalRequest([Required, RegularExpression("Approved|ChangesRequested|Rejected")] string Decision,
    [StringLength(2000)] string? Feedback, [Range(0, 100000)] int? ActivityPoints);
public sealed record ProposalReviewDto(string Decision, string? Feedback, DateTime ReviewedAt);
public sealed record ProposalDto(string Id, string ClubId, string Title, string Objective, string Description, string Venue,
    DateTime StartTime, DateTime EndTime, int Capacity, string Status, IReadOnlyList<ProposalReviewDto> Reviews);
public sealed record CreateEventRequest([Required, StringLength(256)] string Title,
    [Required, StringLength(10000)] string Description, [Required, StringLength(256)] string Venue,
    DateTimeOffset StartTime, DateTimeOffset EndTime, [Range(1, 100000)] int Capacity, bool IsMandatory,
    DateTimeOffset? ResponseDeadline, [Range(0, 10000)] int RequiredClubCount = 0,
    [Range(0, 10000)] int MinimumMembersPerClub = 0, [MaxLength(100)] string[]? FieldIds = null);
public sealed record RegistrationDto(string Id, string EventId, DateTime RegisteredAt);
public sealed record RosterItemDto(string UserId, string FullName, bool? Present, string? AttendanceId);
public sealed record AttendanceItem([Required, StringLength(500)] string UserId, bool Present);
public sealed record RecordAttendanceRequest([Required, MinLength(1), MaxLength(1000)] AttendanceItem[] Entries);
public sealed record ConfirmResultDto(string EventId, int StudentsAwarded, DateTime ConfirmedAt);
public sealed record InviteClubsRequest([MaxLength(1000)] string[]? ClubIds, bool AllActive = false);
public sealed record InvitationResponseRequest(bool Accept, [Range(1, 100000)] int? ExpectedMembers,
    [StringLength(2000)] string? Reason);
public sealed record UpdateProgressRequest([Required, RegularExpression("Preparing|Completed")] string Progress);
public sealed record InvitationDto(string EventId, string ClubId, string Status, string? Progress, int? ExpectedMembers, string Note);
public sealed record EventProgressDto(int Invited, int Accepted, int Completed, int RequiredClubs, IReadOnlyList<InvitationDto> Invitations);
public sealed record RankedClubDto(string ClubId, string Name, double Score, bool FieldMatches, int ActiveMembers, double CompletionRate);
