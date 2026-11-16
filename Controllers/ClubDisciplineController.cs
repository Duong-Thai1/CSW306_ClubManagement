using ClubManagement.Dtos;
using ClubManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Controllers;

[ApiController, Route("api")]
public sealed class ClubDisciplineController(ClubDisciplineService work) : ControllerBase
{
    [HttpPost("clubs/{clubId}/absence-requests")]
    public Task<AbsenceDto> RequestAbsence(string clubId, SubmitAbsenceRequest input, CancellationToken ct) =>
        work.SubmitAbsenceAsync(clubId, input, ct);

    [HttpGet("clubs/{clubId}/absence-requests")]
    public Task<IReadOnlyList<AbsenceDto>> Absences(string clubId, [FromQuery] PageFilter page, CancellationToken ct) =>
        work.AbsencesAsync(clubId, page, ct);

    [HttpPost("clubs/{clubId}/absence-requests/{id}/decision")]
    public Task<AbsenceDto> ReviewAbsence(string clubId, string id, AbsenceDecisionRequest input, CancellationToken ct) =>
        work.ReviewAbsenceAsync(clubId, id, input.Approve, ct);

    [HttpPost("clubs/{clubId}/members/{memberId}/penalties")]
    public Task<PenaltyDto> Penalize(string clubId, string memberId, CreatePenaltyRequest input, CancellationToken ct) =>
        work.PenalizeAsync(clubId, memberId, input, ct);

    [HttpPost("clubs/{clubId}/penalties/{id}/appeals")]
    public Task<PenaltyDto> Appeal(string clubId, string id, AppealRequest input, CancellationToken ct) =>
        work.AppealAsync(clubId, id, input, ct);

    [HttpPost("clubs/{clubId}/penalties/{id}/decision")]
    public Task<PenaltyDto> Resolve(string clubId, string id, PenaltyDecisionRequest input, CancellationToken ct) =>
        work.ResolveAsync(clubId, id, input, ct);

    [HttpGet("clubs/{clubId}/members/me/contribution")]
    public Task<ContributionDto> MyContribution(string clubId, [FromQuery] PageFilter page, CancellationToken ct) =>
        work.ContributionAsync(clubId, null, page, ct);

    [HttpGet("clubs/{clubId}/members/{memberId}/contribution")]
    public Task<ContributionDto> Contribution(string clubId, string memberId, [FromQuery] PageFilter page, CancellationToken ct) =>
        work.ContributionAsync(clubId, memberId, page, ct);
}
