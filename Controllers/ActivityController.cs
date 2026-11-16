using ClubManagement.Dtos;
using ClubManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Controllers;

[ApiController, Route("api")]
public sealed class ActivityController(ActivityQueries activity) : ControllerBase
{
    [Authorize(Policy = "Student"), HttpGet("me/activity-points")]
    public Task<ActivityPointSummary> Points([FromQuery] PageFilter page, CancellationToken ct) => activity.PointsAsync(page, ct);

    [HttpGet("me/notifications")]
    public Task<IReadOnlyList<NotificationDto>> Notifications([FromQuery] PageFilter page, CancellationToken ct) => activity.NotificationsAsync(page, ct);

    [HttpPut("me/notifications/{id}/read")]
    public async Task<IActionResult> Read(string id, CancellationToken ct)
    {
        await activity.ReadNotificationAsync(id, ct);
        return NoContent();
    }

    [HttpPost("clubs/{clubId}/announcements")]
    public Task<AnnouncementDto> Announce(string clubId, AnnouncementRequest input, CancellationToken ct) =>
        activity.AnnounceAsync(clubId, input, ct);

    [HttpGet("clubs/{clubId}/announcements")]
    public Task<IReadOnlyList<AnnouncementDto>> Announcements(string clubId, [FromQuery] PageFilter page, CancellationToken ct) =>
        activity.AnnouncementsAsync(clubId, page, ct);

    [Authorize(Policy = "Staff"), HttpGet("reports/event-participation")]
    public Task<IReadOnlyList<EventReportDto>> Report(string? clubId, DateTimeOffset from, DateTimeOffset to, [FromQuery] PageFilter page, CancellationToken ct) =>
        activity.ReportAsync(clubId, from, to, page, ct);
}
