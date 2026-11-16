using ClubManagement.Dtos;
using ClubManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Controllers;

[ApiController, Route("api")]
public sealed class EventsController(EventService events) : ControllerBase
{
    [HttpGet("events")]
    public Task<IReadOnlyList<EventDto>> List([FromQuery] EventFilter filter, CancellationToken ct) => events.ListAsync(filter, ct);

    [HttpGet("events/{id}")]
    public Task<EventDto> Detail(string id, CancellationToken ct) => events.DetailAsync(id, ct);

    [Authorize(Policy = "Staff"), HttpPost("staff/events")]
    public Task<EventDto> CreateStaff(CreateEventRequest input, CancellationToken ct) => events.CreateAsync(null, input, ct);

    [HttpPost("clubs/{clubId}/events")]
    public Task<EventDto> CreateInternal(string clubId, CreateEventRequest input, CancellationToken ct) => events.CreateAsync(clubId, input, ct);
}
