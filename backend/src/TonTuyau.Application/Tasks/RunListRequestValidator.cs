using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Tasks;

public sealed class RunListRequestValidator : IValidator<RunListRequest>
{
    public void Validate(RunListRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();
        RequestRules.OneOf(errors, "task", value.Task, ApiValues.Tasks);
        if (value.Before is not null && !LibraryPaths.IsRunId(value.Before))
        {
            errors.Add("before", "must be a run id (the nextCursor of the previous page).");
        }

        RequestRules.Size(errors, value.Size, TaskService.MaxRunsPerPage);
        errors.ThrowIfAny(RequestRules.Subject);
    }
}
