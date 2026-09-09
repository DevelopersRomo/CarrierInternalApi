using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InternalCarrierApp.API.DTOs;
using InternalCarrierApp.API.Services;

namespace InternalCarrierApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController(INovuService novu) : ControllerBase
{
    // GET api/notifications/inbox-identity
    /// <summary>
    /// Credentials for the caller's own Novu Inbox. Serving the application identifier
    /// from here instead of a frontend build variable keeps the two ends from drifting
    /// apart across environments.
    /// </summary>
    [HttpGet("inbox-identity")]
    public ActionResult<NovuInboxIdentityDto> GetInboxIdentity()
    {
        var subscriberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subscriberId))
            return Unauthorized(new { message = "El token no identifica a un usuario." });

        var subscriberHash = novu.CreateSubscriberHash(subscriberId);
        if (subscriberHash is null || novu.ApplicationIdentifier is null)
        {
            // 503 rather than 500: the API itself is healthy, the notification provider
            // simply has no credentials in this environment. The client hides the inbox
            // instead of showing an error.
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Las notificaciones no están configuradas en este entorno." });
        }

        return Ok(new NovuInboxIdentityDto(subscriberId, subscriberHash, novu.ApplicationIdentifier));
    }
}
