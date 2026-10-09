using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using VIHouse.Business;

namespace VIHouse.WebUI.Helpers;

/// <summary>Shared events for the Google and Apple sign-in handlers.</summary>
public static class ExternalSignIn
{
    /// <summary>
    /// The person cancelled at the provider, or the round trip broke (an expired correlation
    /// cookie, a denied consent). Without this the handler throws and the visitor gets the error
    /// page; with it they land back on the external-login callback, which says so in their language
    /// and returns them to the sign-in page.
    /// </summary>
    public static Task Failed(RemoteFailureContext context)
    {
        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ExternalSignIn))
            .LogWarning(context.Failure, "{Scheme} sign-in did not complete.", context.Scheme.Name);

        // The callback URL travels inside the protected state, so it is ours; the local-path check is
        // belt and braces. When the state itself could not be read there is nothing to return to.
        var callback = context.Properties?.RedirectUri;
        var target = callback is ['/', not '/' and not '\\', ..]
            ? QueryHelpers.AddQueryString(callback, "remoteError", "failed")
            : SiteUrls.Login;
        context.Response.Redirect(target);
        context.HandleResponse();
        return Task.CompletedTask;
    }
}
