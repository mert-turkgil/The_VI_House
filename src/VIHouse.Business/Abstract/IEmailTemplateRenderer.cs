namespace VIHouse.Business.Abstract;

/// <summary>
/// Renders a named template to HTML. Implemented in VIHouse.WebUI (RazorEmailTemplateRenderer) since
/// it needs the Razor view engine — Business stays free of any MVC dependency, same reasoning as
/// IPaymentProvider keeping Stripe.net out of the interface.
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <param name="culture">One of SiteCultures.Names — the renderer sets this as the ambient
    /// thread culture for the duration of the render, so the template's own
    /// IStringLocalizer&lt;SharedResource&gt; calls (including its ViewData["Subject"], if it sets
    /// one) resolve in the recipient's language, not whatever culture happened to be ambient when
    /// the caller built the rest of the email.</param>
    Task<RenderedEmail> RenderAsync<TModel>(string templateKey, TModel model, string culture, CancellationToken ct = default);
}

/// <param name="Html">The rendered body.</param>
/// <param name="Subject">The template's own localized subject (ViewData["Subject"]), if it set one —
/// null for a template not yet migrated to it, in which case the caller's literal subject is used.</param>
public record RenderedEmail(string Html, string? Subject);
