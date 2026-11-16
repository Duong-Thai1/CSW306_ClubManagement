using ClubManagement.Dtos;
using ClubManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Controllers;

[ApiController, Route("api/events/{eventId}")]
public sealed class EventParticipationController(EventRegistrationService registrations, EventResultService results) : ControllerBase
{
    [Authorize(Policy = "Student"), HttpPost("registrations")]
    public Task<RegistrationDto> Register(string eventId, CancellationToken ct) => registrations.RegisterAsync(eventId, ct);

    [Authorize(Policy = "Student"), HttpDelete("registrations/me")]
    public async Task<IActionResult> Cancel(string eventId, CancellationToken ct)
    {
        await registrations.CancelAsync(eventId, ct);
        return NoContent();
    }

    [HttpGet("registrations")]
    public Task<IReadOnlyList<RosterItemDto>> Roster(string eventId, string? clubId, [FromQuery] PageFilter page, CancellationToken ct)
        => registrations.RosterAsync(eventId, clubId, page, ct);

    [HttpPut("attendance")]
    public async Task<IActionResult> Record(string eventId, string? clubId, RecordAttendanceRequest input, CancellationToken ct)
    {
        await results.RecordAttendanceAsync(eventId, clubId, input, ct);
        return NoContent();
    }

    [Authorize(Policy = "Staff"), HttpPost("confirm-results")]
    public Task<ConfirmResultDto> Confirm(string eventId, CancellationToken ct) => results.ConfirmAsync(eventId, ct);
}
