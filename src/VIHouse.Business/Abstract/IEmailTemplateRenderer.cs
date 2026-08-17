namespace VIHouse.Business.Abstract;

/// <summary>
/// Renders a named template to HTML. Implemented in VIHouse.WebUI (RazorEmailTemplateRenderer) since
/// it needs the Razor view engine — Business stays free of any MVC dependency, same reasoning as
/// IPaymentProvider keeping Stripe.net out of the interface.
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync<TModel>(string templateKey, TModel model, CancellationToken ct = default);
}
