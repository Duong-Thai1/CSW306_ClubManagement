using ClubManagement.Dtos;
using ClubManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Controllers;

[ApiController, Route("api/clubs")]
public sealed class ClubsController(ClubService clubs) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ClubDto>> List(string? search, string? fieldId, bool? recruiting, [FromQuery] PageFilter page, CancellationToken ct)
        => clubs.ListAsync(search, fieldId, recruiting, page, ct);

    [Authorize(Policy = "Staff"), HttpPost]
    public Task<ClubDto> Create(SaveClubRequest input, CancellationToken ct) => clubs.SaveAsync(null, input, ct);

    [Authorize(Policy = "Staff"), HttpPut("{clubId}")]
    public Task<ClubDto> Update(string clubId, SaveClubRequest input, CancellationToken ct) => clubs.SaveAsync(clubId, input, ct);

    [Authorize(Policy = "Staff"), HttpPut("{clubId}/status")]
    public async Task<IActionResult> Status(string clubId, ClubStatusRequest input, CancellationToken ct)
    {
        await clubs.ChangeStatusAsync(clubId, input.Status, ct);
        return NoContent();
    }

    [Authorize(Policy = "Staff"), HttpPut("{clubId}/owner")]
    public async Task<IActionResult> AssignOwner(string clubId, AssignOwnerRequest input, CancellationToken ct)
    {
        await clubs.AssignOwnerAsync(clubId, input.UserId, ct);
        return NoContent();
    }

    [HttpGet("fields")]
    public Task<IReadOnlyList<FieldDto>> Fields(CancellationToken ct) => clubs.FieldsAsync(ct);

    [Authorize(Policy = "Staff"), HttpPost("fields")]
    public Task<FieldDto> CreateField(FieldRequest input, CancellationToken ct) => clubs.CreateFieldAsync(input, ct);

    [Authorize(Policy = "Student"), HttpPost("{clubId}/join-requests")]
    public Task<JoinRequestDto> Join(string clubId, JoinClubRequest input, CancellationToken ct) => clubs.JoinAsync(clubId, input, ct);

    [HttpGet("{clubId}/join-requests")]
    public Task<IReadOnlyList<JoinRequestDto>> JoinRequests(string clubId, [FromQuery] PageFilter page, CancellationToken ct)
        => clubs.JoinRequestsAsync(clubId, page, ct);

    [HttpPost("{clubId}/join-requests/{requestId}/decision")]
    public async Task<IActionResult> Review(string clubId, string requestId, ReviewJoinRequest input, CancellationToken ct)
    {
        await clubs.ReviewJoinAsync(clubId, requestId, input, ct);
        return NoContent();
    }

    [HttpDelete("{clubId}/members/me")]
    public async Task<IActionResult> Leave(string clubId, CancellationToken ct)
    {
        await clubs.LeaveAsync(clubId, ct);
        return NoContent();
    }

    [HttpPost("{clubId}/members/{memberId}/expulsion")]
    public async Task<IActionResult> Expel(string clubId, string memberId, ExpelMemberRequest input, CancellationToken ct)
    {
        await clubs.ExpelAsync(clubId, memberId, input, ct);
        return NoContent();
    }

    [HttpPost("{clubId}/recruitment-posts")]
    public Task<RecruitmentPostDto> PostRecruitment(string clubId, RecruitmentPostRequest input, CancellationToken ct)
        => clubs.PostRecruitmentAsync(clubId, input, ct);

    [HttpGet("{clubId}/recruitment-posts")]
    public Task<IReadOnlyList<RecruitmentPostDto>> Recruitment(string clubId, [FromQuery] PageFilter page, CancellationToken ct)
        => clubs.RecruitmentAsync(clubId, page, ct);
}
