namespace TonTuyau.Application.Dashboard;

/// <param name="Period">day | week | month (default) | year | all.</param>
public sealed record ImportHistoryRequest(string? Period);
