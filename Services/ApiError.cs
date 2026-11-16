using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClubManagement.Services;

public sealed class ApiError(int status, string message) : Exception(message)
{
    public int Status { get; } = status;

    public static ApiError Bad(string detail) => new(400, detail);
    public static ApiError Unauthorized() => new(401, "Invalid credentials or session.");
    public static ApiError Forbidden() => new(403, "You do not have access to this resource.");
    public static ApiError Missing(string detail) => new(404, detail);
    public static ApiError Conflict(string detail) => new(409, detail);
}

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var error = exception switch
        {
            ApiError apiError => apiError,
            Microsoft.EntityFrameworkCore.DbUpdateException
                { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } }
                => ApiError.Conflict("This resource already exists."),
            Microsoft.Data.SqlClient.SqlException { Number: 51001 }
                => ApiError.Conflict("The resource is busy. Try again."),
            _ => null
        };
        if (error is null) return false;

        await Results.Problem(statusCode: error.Status, detail: error.Message).ExecuteAsync(context);
        return true;
    }
}
