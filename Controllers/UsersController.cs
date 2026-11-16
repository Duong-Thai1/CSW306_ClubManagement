using ClubManagement.Dtos;
using ClubManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Controllers;

[ApiController, Authorize(Policy = "Staff"), Route("api/users")]
public sealed class UsersController(UserAdminService users) : ControllerBase
{
    [HttpPost]
    public Task<UserDto> Create(CreateUserRequest input, CancellationToken ct) => users.CreateAsync(input, ct);

    [HttpPut("{userId}")]
    public Task<UserDto> Update(string userId, UpdateUserRequest input, CancellationToken ct) => users.UpdateAsync(userId, input, ct);
}
