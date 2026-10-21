using System.Data.Common;

namespace TonTuyau.Application.Sync;

/// <summary>Why a run failed, as its record and log say it.</summary>
internal static class RunErrors
{
    /// <summary>
    /// The error's message; a database error's own when the data layer wrapped it (EF Core's "An error occurred while
    /// saving the entity changes" says nothing of what PostgreSQL refused).
    /// </summary>
    public static string ReasonOf(Exception error) =>
        error.InnerException is DbException inner ? inner.Message : error.Message;
}
