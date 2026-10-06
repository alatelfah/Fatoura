using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Fatoura.Api.Infrastructure;

/// <summary>Business-rule failures, mapped to RFC 7807 problem responses by <see cref="ProblemExceptionHandler"/>.</summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

public sealed class NotFoundException(string what) : AppException($"{what} was not found.")
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public sealed class ConflictException(string message, string? code = null) : AppException(message)
{
    public string? Code { get; } = code;

    public override int StatusCode => StatusCodes.Status409Conflict;
}

public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
}

/// <summary>Field-level validation errors (400 with an "errors" dictionary, like model validation).</summary>
public sealed class InvalidRequestException(IDictionary<string, string[]> errors)
    : AppException("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public override int StatusCode => StatusCodes.Status400BadRequest;

    public static InvalidRequestException For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>Collects field errors and throws a single <see cref="InvalidRequestException"/>.</summary>
public sealed class FieldValidator
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool IsValid => _errors.Count == 0;

    public FieldValidator Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
        {
            _errors[field] = list = [];
        }

        list.Add(message);
        return this;
    }

    public FieldValidator Require(bool condition, string field, string message) => condition ? this : Add(field, message);

    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new InvalidRequestException(_errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()));
        }
    }
}

internal sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException app)
        {
            return false;
        }

        httpContext.Response.StatusCode = app.StatusCode;
        ProblemDetails problem = app is InvalidRequestException v
            ? new HttpValidationProblemDetails(v.Errors) { Status = app.StatusCode, Title = app.Message }
            : new ProblemDetails { Status = app.StatusCode, Title = app.Message };
        if (app is ConflictException { Code: not null } c)
        {
            problem.Extensions["code"] = c.Code;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}

/// <summary>
/// Built-in request validation reports keys like "Lines[0].Description"; the API's own rules use "lines[0].description".
/// Normalising every segment to camelCase gives clients one convention.
/// </summary>
internal static class ProblemKeys
{
    public static void CamelCase(ProblemDetails problem)
    {
        if (problem is not HttpValidationProblemDetails v)
        {
            return;
        }

        var renamed = v.Errors.ToDictionary(kv => Convert(kv.Key), kv => kv.Value, StringComparer.Ordinal);
        v.Errors.Clear();
        foreach (var (key, value) in renamed)
        {
            v.Errors[key] = value;
        }
    }

    private static string Convert(string key) =>
        string.Join('.', key.Split('.').Select(s => s.Length > 0 && char.IsUpper(s[0]) ? char.ToLowerInvariant(s[0]) + s[1..] : s));
}
