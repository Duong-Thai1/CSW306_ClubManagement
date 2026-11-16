using System.ComponentModel.DataAnnotations;

namespace ClubManagement.Dtos;

public sealed record CreateTaskRequest([Required, StringLength(256)] string Title,
    [Required, StringLength(4000)] string Description, DateTimeOffset Deadline,
    [Required, RegularExpression("Low|Normal|High")] string Priority,
    [Range(0, 1000)] int Points, [StringLength(500)] string? EventId,
    [MaxLength(1000)] string[]? MembershipIds, bool AllMembers,
    [Range(1, 30)] int? RepeatEveryDays, DateTimeOffset? RepeatUntil);
public sealed record TaskDto(string AssignmentId, string TaskId, string ClubId, string Title, string Description,
    DateTime Deadline, string Priority, string Status, int Points);
public sealed record TaskStatusRequest([Required, RegularExpression("Todo|InProgress|Done")] string Status);
public sealed record SubmitAbsenceRequest([StringLength(500)] string? EventId,
    [StringLength(500)] string? TaskAssignmentId, [Required, StringLength(2000)] string Reason,
    [StringLength(500)] string? EvidenceReference);
public sealed record AbsenceDto(string Id, string MembershipId, string? EventId, string? TaskAssignmentId,
    string Status, bool IsLate, string Reason, string? EvidenceReference);
public sealed record AbsenceDecisionRequest(bool Approve);
public sealed record CreatePenaltyRequest([StringLength(500)] string? AttendanceId, [StringLength(500)] string? TaskAssignmentId,
    [Required, StringLength(2000)] string Reason);
public sealed record PenaltyDecisionRequest(bool Waive, [Required, StringLength(2000)] string Reason);
public sealed record AppealRequest([Required, StringLength(2000)] string Reason);
public sealed record PenaltyDto(string Id, string Status, string Reason, int Deduction, DateTime CreatedAt,
    string? ResolutionReason, string? AppealReason);
public sealed record ContributionDto(int Points, int ViolationsThisSemester, bool Warning, bool ReviewExpulsion,
    IReadOnlyList<PenaltyDto> Penalties);
