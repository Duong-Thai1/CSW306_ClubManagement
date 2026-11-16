using ClubManagement.Dtos;
using ClubManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Controllers;

[ApiController, Route("api")]
public sealed class EventProposalsController(EventProposalService proposals) : ControllerBase
{
    [HttpPost("clubs/{clubId}/event-proposals")]
    public Task<ProposalDto> Create(string clubId, SaveProposalRequest input, CancellationToken ct) =>
        proposals.SaveAsync(clubId, null, input, ct);

    [HttpPut("clubs/{clubId}/event-proposals/{id}")]
    public Task<ProposalDto> Update(string clubId, string id, SaveProposalRequest input, CancellationToken ct) =>
        proposals.SaveAsync(clubId, id, input, ct);

    [HttpGet("event-proposals/{id}")]
    public Task<ProposalDto> Detail(string id, CancellationToken ct) => proposals.DetailAsync(id, ct);

    [HttpGet("clubs/{clubId}/event-proposals")]
    public Task<IReadOnlyList<ProposalDto>> ForClub(string clubId, [FromQuery] PageFilter page, CancellationToken ct) =>
        proposals.ListAsync(clubId, false, page, ct);

    [Authorize(Policy = "Staff"), HttpGet("event-proposals")]
    public Task<IReadOnlyList<ProposalDto>> ForStaff(bool pending, [FromQuery] PageFilter page, CancellationToken ct) =>
        proposals.ListAsync(null, pending, page, ct);

    [HttpPost("event-proposals/{id}/submit")]
    public async Task<IActionResult> Submit(string id, CancellationToken ct)
    {
        await proposals.SubmitAsync(id, ct);
        return NoContent();
    }

    [Authorize(Policy = "Staff"), HttpPost("event-proposals/{id}/decisions")]
    public async Task<IActionResult> Review(string id, ReviewProposalRequest input, CancellationToken ct) =>
        Ok(new { EventId = await proposals.ReviewAsync(id, input, ct) });
}
