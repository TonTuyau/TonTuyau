using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.ApiKeys;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// The admin's API keys (1.5): list, create (the secret given once), enable, disable, delete. A logged-in admin only
/// (the default policy): a key gets 403, it can't make or unlock other keys.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/api-keys")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, Problems.ContentType)]
public sealed class ApiKeysController(ApiKeyService keys) : ControllerBase
{
    /// <summary>The largest body taken: a name fits in far less.</summary>
    public const int MaxRequestBytes = 4096;

    /// <summary>Every key, the latest created first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ApiKeyResponse>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ApiKeyResponse>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ApiKey> list = await keys
            .ListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. list.Select(k => (ApiKeyResponse)k)];
    }

    /// <summary>Creates a key, enabled: 201 with its secret, given this once. 409 when a key has the name already.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType<CreatedApiKeyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<IActionResult> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        CreatedApiKey created = await keys
            .CreateAsync(request.Name, request.Kind, Account(), cancellationToken)
            .ConfigureAwait(false);
        return StatusCode(StatusCodes.Status201Created, (CreatedApiKeyResponse)created);
    }

    /// <summary>Lets the key in again.</summary>
    [HttpPost("{id:int}/enable")]
    [ProducesResponseType<ApiKeyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<ApiKeyResponse> EnableAsync(int id, CancellationToken cancellationToken) =>
        (ApiKeyResponse)await keys
            .SetEnabledAsync(id, enabled: true, Account(), cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Refuses the key from its next request on, until it's enabled again.</summary>
    [HttpPost("{id:int}/disable")]
    [ProducesResponseType<ApiKeyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<ApiKeyResponse> DisableAsync(int id, CancellationToken cancellationToken) =>
        (ApiKeyResponse)await keys
            .SetEnabledAsync(id, enabled: false, Account(), cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Deletes the key for good.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await keys
            .DeleteAsync(id, Account(), cancellationToken)
            .ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>The logged-in account's name ([Authorize]: there is one).</summary>
    private string Account() =>
        User.Identity?.Name ?? throw new InvalidOperationException("A logged-in request without a name.");
}
