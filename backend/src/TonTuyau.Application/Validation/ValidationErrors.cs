namespace TonTuyau.Application.Validation;

/// <summary>Collects broken rules, then throws them all at once.</summary>
public sealed class ValidationErrors
{
    private readonly List<ValidationError> _errors = [];

    public void Add(string field, string message) => _errors.Add(new ValidationError(field, message));

    public void ThrowIfAny(string subject)
    {
        if (_errors.Count > 0)
        {
            throw new ValidationException(subject, _errors.ToArray());
        }
    }
}
