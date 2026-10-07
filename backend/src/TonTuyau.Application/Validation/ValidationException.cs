namespace TonTuyau.Application.Validation;

/// <summary>Thrown by validators when data is broken. Carries every broken rule, not just the first.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string subject, IReadOnlyList<ValidationError> errors)
        : base(BuildMessage(subject, errors))
    {
        Subject = subject;
        Errors = errors;
    }

    public string Subject { get; }

    public IReadOnlyList<ValidationError> Errors { get; }

    private static string BuildMessage(string subject, IReadOnlyList<ValidationError> errors) =>
        $"Invalid {subject}: " + string.Join("; ", errors.Select(e => $"{e.Field}: {e.Message}"));
}
