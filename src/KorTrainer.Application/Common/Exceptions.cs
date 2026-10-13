namespace KorTrainer.Application.Common;

/// <summary>Input rejected at the application boundary; mapped to HTTP 400 with field errors.</summary>
public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static ValidationFailedException For(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>A requested resource does not exist; mapped to HTTP 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>A concurrent change won the race (e.g. the same card answered twice); mapped to HTTP 409.</summary>
public sealed class ConflictException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Collects field errors and throws them together.</summary>
public sealed class Validator
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public Validator Require(bool condition, string field, string message)
    {
        if (!condition)
        {
            if (!_errors.TryGetValue(field, out var messages))
            {
                messages = [];
                _errors[field] = messages;
            }

            messages.Add(message);
        }

        return this;
    }

    public void ThrowIfInvalid()
    {
        if (_errors.Count > 0)
        {
            throw new ValidationFailedException(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}
