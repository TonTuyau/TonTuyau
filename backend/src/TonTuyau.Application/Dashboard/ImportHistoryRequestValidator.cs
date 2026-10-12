using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Dashboard;

public sealed class ImportHistoryRequestValidator : IValidator<ImportHistoryRequest>
{
    public void Validate(ImportHistoryRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();
        RequestRules.OneOf(errors, "period", value.Period, ApiValues.ImportPeriods);
        errors.ThrowIfAny(RequestRules.Subject);
    }
}
