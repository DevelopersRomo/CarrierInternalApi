using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternalCarrierApp.API.Services;

/// <summary>
/// Who receives a notification. Carrying the profile fields next to the id lets Novu
/// upsert the subscriber on the trigger itself, so a user never has to be registered
/// in Novu ahead of time.
/// </summary>
public record NovuRecipient(
    string  SubscriberId,
    string? Email     = null,
    string? FirstName = null,
    string? LastName  = null);

public interface INovuService
{
    /// <summary>False when no secret key is configured; every call then no-ops.</summary>
    bool IsConfigured { get; }

    /// <summary>Public identifier the Inbox needs; null when Novu is not configured.</summary>
    string? ApplicationIdentifier { get; }

    /// <summary>Fires one workflow for one subscriber. Never throws on Novu failures.</summary>
    Task<bool> TriggerAsync(
        string            workflowId,
        NovuRecipient     recipient,
        object?           payload           = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fires one workflow for many subscribers in a single request. Novu fans the
    /// event out itself, so this is one round trip rather than one per recipient.
    /// </summary>
    Task<bool> TriggerAsync(
        string                     workflowId,
        IEnumerable<NovuRecipient> recipients,
        object?                    payload           = null,
        CancellationToken          cancellationToken = default);

    /// <summary>
    /// HMAC-SHA256 of the subscriber id, required by the Inbox in secure mode. Without
    /// it, knowing another user's subscriber id would be enough to read their inbox.
    /// </summary>
    string? CreateSubscriberHash(string subscriberId);
}

public class NovuService(
    HttpClient           http,
    IConfiguration       config,
    ILogger<NovuService> logger) : INovuService
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        // Novu omits absent optional fields rather than sending explicit nulls, which it
        // would otherwise read as "clear this value" on an existing subscriber.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // Workflow templates read payload keys verbatim ({{ payload.title }}), so a DTO
        // with PascalCase properties would silently never match. Lowering the first
        // character keeps a C# record and an anonymous object producing the same keys.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Environment wins over appsettings, matching how the JWT key is resolved, so a
    // deployment can supply the secret without it ever living in a tracked file.
    private readonly string? _secretKey =
        Trimmed(config["NOVU_SECRET_KEY"]) ?? Trimmed(config["Novu:SecretKey"]);

    private readonly string? _applicationIdentifier =
        Trimmed(config["NOVU_APPLICATION_IDENTIFIER"]) ?? Trimmed(config["Novu:ApplicationIdentifier"]);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public bool IsConfigured => _secretKey is not null;

    public string? ApplicationIdentifier => _applicationIdentifier;

    public Task<bool> TriggerAsync(
        string            workflowId,
        NovuRecipient     recipient,
        object?           payload           = null,
        CancellationToken cancellationToken = default) =>
        TriggerAsync(workflowId, [recipient], payload, cancellationToken);

    public async Task<bool> TriggerAsync(
        string                     workflowId,
        IEnumerable<NovuRecipient> recipients,
        object?                    payload           = null,
        CancellationToken          cancellationToken = default)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Novu is not configured; skipping workflow '{WorkflowId}'.", workflowId);
            return false;
        }

        var to = recipients
            .Where(r => !string.IsNullOrWhiteSpace(r.SubscriberId))
            .Select(r => new
            {
                subscriberId = r.SubscriberId,
                email        = Trimmed(r.Email),
                firstName    = Trimmed(r.FirstName),
                lastName     = Trimmed(r.LastName)
            })
            .ToArray();

        if (to.Length == 0)
        {
            logger.LogDebug("Workflow '{WorkflowId}' had no valid recipients.", workflowId);
            return false;
        }

        var body = new { name = workflowId, to, payload = payload ?? new { } };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/events/trigger")
            {
                Content = JsonContent.Create(body, options: PayloadOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", _secretKey);

            using var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Triggered Novu workflow '{WorkflowId}' for {RecipientCount} subscriber(s).",
                    workflowId, to.Length);
                return true;
            }

            // A workflow that is missing, inactive, or missing a step answers 4xx. That
            // is a configuration gap in the Novu dashboard, not a bug in the caller.
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Novu rejected workflow '{WorkflowId}' with {StatusCode}: {Detail}",
                workflowId, (int)response.StatusCode, detail);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   && !cancellationToken.IsCancellationRequested)
        {
            // A notification is never the reason a business operation fails, so a
            // transport error or a Novu timeout is logged and swallowed. The caller's
            // own cancellation still propagates, because that is the request going away.
            logger.LogError(ex, "Could not reach Novu to trigger workflow '{WorkflowId}'.", workflowId);
            return false;
        }
    }

    public string? CreateSubscriberHash(string subscriberId)
    {
        if (_secretKey is null || string.IsNullOrWhiteSpace(subscriberId))
            return null;

        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_secretKey),
            Encoding.UTF8.GetBytes(subscriberId));

        // Novu compares against Node's `digest('hex')`, which is lower case.
        return Convert.ToHexStringLower(hash);
    }
}
