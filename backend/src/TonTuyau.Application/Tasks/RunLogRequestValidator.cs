using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Tasks;

public sealed class RunLogRequestValidator : IValidator<RunLogRequest>
{
    public void Validate(RunLogRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();
        if (value.After is not null && value.After.Value < 0)
        {
            errors.Add("after", "must be 0 or more.");
        }

        RequestRules.Size(errors, value.Size, TaskService.MaxLogLinesPerPage);
        errors.ThrowIfAny(RequestRules.Subject);
    }
}
