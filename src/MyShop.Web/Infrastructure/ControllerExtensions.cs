using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MyShop.Domain.Common;

namespace MyShop.Web.Infrastructure;

public enum FlashKind { Success, Danger }

public static class ControllerExtensions
{
    public const string FlashKey = "Flash";
    public const string FlashKindKey = "FlashKind";

    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? throw new InvalidOperationException("The user is not authenticated."));

    /// <summary>Stores a one-shot message in TempData (cookie-backed, so it stays stateless).</summary>
    public static void Flash(this Controller controller, string message, FlashKind kind = FlashKind.Success)
    {
        controller.TempData[FlashKey] = message;
        controller.TempData[FlashKindKey] = kind.ToString().ToLowerInvariant();
    }

    /// <summary>Post-Redirect-Get: flashes the outcome of a use case, then redirects.</summary>
    public static IActionResult RedirectWithOutcome<T>(this Controller controller, Result<T> result, Func<T, string> successMessage, IActionResult redirect) =>
        result.Match(
            value => { controller.Flash(successMessage(value)); return redirect; },
            error => { controller.Flash(error.Message, FlashKind.Danger); return redirect; });

    /// <summary>For GETs: renders the value or maps the domain error to an HTTP status.</summary>
    public static IActionResult ViewOrError<T>(this Controller controller, Result<T> result, Func<T, IActionResult> onSuccess) =>
        result.Match(onSuccess, error => error.Kind switch
        {
            ErrorKind.NotFound => controller.NotFound(),
            ErrorKind.Conflict => controller.Conflict(error.Message),
            _ => controller.BadRequest(error.Message)
        });

    /// <summary>Copies a validation failure onto the model state so the form can be redisplayed.</summary>
    public static void AddError(this Controller controller, Error error) =>
        controller.ModelState.AddModelError(string.Empty, error.Message);
}
