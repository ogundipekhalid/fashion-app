namespace FashionCommerce.Shared.Wrappers;

/// <summary>
/// Standard Response Format requirement: every controller action returns
/// this shape, success or failure, so API consumers (web, and later the
/// .NET MAUI apps) parse one envelope everywhere instead of guessing.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
    public IEnumerable<string>? Errors { get; init; }

    public static ApiResponse<T> SuccessResponse(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> FailureResponse(string message, IEnumerable<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}

/// <summary>Non-generic variant for actions that return no payload (e.g. Logout, ResetPassword).</summary>
public class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public IEnumerable<string>? Errors { get; init; }

    public static ApiResponse SuccessResponse(string? message = null) => new() { Success = true, Message = message };

    public static ApiResponse FailureResponse(string message, IEnumerable<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}

/// <summary>Wraps PaginatedList<T> for list endpoints, carrying paging metadata alongside the standard envelope.</summary>
public class PaginatedApiResponse<T> : ApiResponse<IEnumerable<T>>
{
    public int PageNumber { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool HasNextPage { get; init; }
}
