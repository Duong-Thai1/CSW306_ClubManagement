using System.ComponentModel.DataAnnotations;
using ClubManagement.Dtos;
using ClubManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Controllers;

[ApiController, Route("api/staff/events/{eventId}")]
public sealed class ClubInvitationsController(ClubInvitationService invitations) : ControllerBase
{
    [HttpGet("/api/clubs/{clubId}/invitations")]
    public Task<IReadOnlyList<InvitationDto>> ForClub(string clubId, CancellationToken ct) => invitations.ForClubAsync(clubId, ct);

    [Authorize(Policy = "Staff"), HttpPost("invitations")]
    public async Task<IActionResult> Invite(string eventId, InviteClubsRequest input, CancellationToken ct)
    {
        await invitations.InviteAsync(eventId, input, ct);
        return NoContent();
    }

    [Authorize(Policy = "Staff"), HttpGet("recommendations")]
    public Task<IReadOnlyList<RankedClubDto>> Recommend(string eventId, CancellationToken ct, [FromQuery, Range(1, 100)] int count = 10)
        => invitations.RecommendAsync(eventId, count, ct);

    [Authorize(Policy = "Staff"), HttpGet("progress")]
    public Task<EventProgressDto> Progress(string eventId, CancellationToken ct) => invitations.ProgressAsync(eventId, ct);

    [HttpPut("invitations/{clubId}/response")]
    public Task<InvitationDto> Respond(string eventId, string clubId, InvitationResponseRequest input, CancellationToken ct) =>
        invitations.RespondAsync(eventId, clubId, input, ct);

    [HttpPut("invitations/{clubId}/progress")]
    public Task<InvitationDto> UpdateProgress(string eventId, string clubId, UpdateProgressRequest input, CancellationToken ct) =>
        invitations.SetProgressAsync(eventId, clubId, input, ct);
}
