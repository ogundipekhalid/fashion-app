using FluentValidation.Results;

namespace FashionCommerce.Application.Common.Exceptions;

/// <summary>Thrown by ValidationBehavior when FluentValidation rules fail. Mapped to HTTP 400 by API middleware.</summary>
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException() : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures) : this()
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}

/// <summary>Mapped to HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.") { }

    public NotFoundException(string message) : base(message) { }
}

/// <summary>Mapped to HTTP 403. Thrown when a user acts on a resource they don't own/can't access (e.g. Vendor A editing Vendor B's product).</summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() : base("You do not have permission to perform this action.") { }
    public ForbiddenAccessException(string message) : base(message) { }
}

/// <summary>Mapped to HTTP 401. Thrown for invalid credentials, expired/revoked tokens, locked accounts.</summary>
public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message) : base(message) { }
}
