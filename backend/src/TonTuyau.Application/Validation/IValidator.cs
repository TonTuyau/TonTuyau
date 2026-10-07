namespace TonTuyau.Application.Validation;

/// <summary>Checks a value and throws <see cref="ValidationException"/> if it is broken.</summary>
public interface IValidator<in T>
{
    void Validate(T value);
}
