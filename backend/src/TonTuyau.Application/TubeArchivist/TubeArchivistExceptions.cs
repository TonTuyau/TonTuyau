using System.Net;

namespace TonTuyau.Application.TubeArchivist;

/// <summary>Base for every failure talking to TubeArchivist. Messages never contain the API token.</summary>
public class TubeArchivistException : Exception
{
    public TubeArchivistException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}

/// <summary>401/403: missing, wrong or revoked token.</summary>
public sealed class TubeArchivistAuthException(string message, HttpStatusCode statusCode)
    : TubeArchivistException(message, statusCode);

/// <summary>400: TubeArchivist rejected a parameter. <see cref="Errors"/> = parameter → messages.</summary>
public sealed class TubeArchivistBadRequestException(
    string message,
    IReadOnlyDictionary<string, IReadOnlyList<string>> errors)
    : TubeArchivistException(message, HttpStatusCode.BadRequest)
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; } = errors;
}

/// <summary>5xx. Also what TubeArchivist returns for a page past its 10,000-result window.</summary>
public sealed class TubeArchivistServerException(string message, HttpStatusCode statusCode)
    : TubeArchivistException(message, statusCode);

/// <summary>Network error or timeout: TubeArchivist couldn't be reached.</summary>
public sealed class TubeArchivistUnavailableException(string message, Exception innerException)
    : TubeArchivistException(message, null, innerException);

/// <summary>TubeArchivist answered with something that isn't the expected JSON shape.</summary>
public sealed class TubeArchivistProtocolException(
    string message,
    HttpStatusCode? statusCode = null,
    Exception? innerException = null)
    : TubeArchivistException(message, statusCode, innerException);
