namespace MoeezMobile.Api.Models.Common;

/// <summary>
/// Lets the localization filter rewrite Message without knowing the generic argument.
/// </summary>
public interface IApiResponse
{
    string Message { get; set; }
    List<string> Errors { get; set; }
}

/// <summary>Standard response envelope used by every endpoint.</summary>
public class ApiResponse<T> : IApiResponse
{
    public bool Success { get; set; }

    /// <summary>A <see cref="MessageKeys"/> key while in flight; localized text by the time it is serialized.</summary>
    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponse<T> Ok(T data, string messageKey = MessageKeys.Ok) =>
        new() { Success = true, Message = messageKey, Data = data };

    public static ApiResponse<T> Fail(string messageKey, params string[] errors) =>
        new() { Success = false, Message = messageKey, Errors = errors.ToList() };
}

/// <summary>Non-generic helper for endpoints that return no payload.</summary>
public static class ApiResponse
{
    public static ApiResponse<object?> Ok(string messageKey = MessageKeys.Saved) =>
        new() { Success = true, Message = messageKey, Data = null };

    public static ApiResponse<object?> Fail(string messageKey, params string[] errors) =>
        new() { Success = false, Message = messageKey, Errors = errors.ToList() };
}
