using System.ComponentModel.DataAnnotations;

namespace ClubManagement.Dtos;

public sealed record ActivityPointDto(string EventId, string EventTitle, int Points, DateTime AwardedAt);
public sealed record ActivityPointSummary(long Total, IReadOnlyList<ActivityPointDto> Items);
public sealed record NotificationDto(string Id, string? ClubId, string Message, DateTime CreatedAt, DateTime? ReadAt);
public sealed record AnnouncementRequest([Required, StringLength(256)] string Title,
    [Required, StringLength(4000)] string Content, bool IsImportant);
public sealed record AnnouncementDto(string Id, string Title, string Content, bool IsImportant, DateTime CreatedAt);
public sealed record EventReportDto(string EventId, string? ClubId, string Title, DateTime StartTime,
    int Registrations, int Present, int InvitedClubs, int AcceptedClubs);
