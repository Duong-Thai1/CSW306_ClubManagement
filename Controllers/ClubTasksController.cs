using ClubManagement.Dtos;
using ClubManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Controllers;

[ApiController, Route("api")]
public sealed class ClubTasksController(ClubTaskService work) : ControllerBase
{
    [HttpPost("clubs/{clubId}/tasks")]
    public async Task<IActionResult> CreateTask(string clubId, CreateTaskRequest input, CancellationToken ct)
        => Ok(new { TaskId = await work.CreateTaskAsync(clubId, input, ct) });

    [HttpGet("me/tasks")]
    public Task<IReadOnlyList<TaskDto>> MyTasks([FromQuery] PageFilter page, CancellationToken ct) =>
        work.MyTasksAsync(page, ct);

    [HttpPut("me/tasks/{assignmentId}/status")]
    public async Task<IActionResult> UpdateTask(string assignmentId, TaskStatusRequest input, CancellationToken ct)
    {
        await work.UpdateTaskAsync(assignmentId, input, ct);
        return NoContent();
    }

    [HttpGet("clubs/{clubId}/members/progress")]
    public Task<IReadOnlyList<MemberProgressDto>> Progress(string clubId, [FromQuery] PageFilter page, CancellationToken ct) =>
        work.ProgressAsync(clubId, page, ct);
}
