namespace InternalCarrierApp.API.DTOs;

/// <summary>
/// Everything the Novu Inbox needs to open a session for the signed-in user.
/// The subscriber id is derived from the bearer token and never accepted from the
/// client: handing out a hash for a caller-supplied id would let anyone read any inbox.
/// </summary>
public record NovuInboxIdentityDto(
    string SubscriberId,
    string SubscriberHash,
    string ApplicationIdentifier);
