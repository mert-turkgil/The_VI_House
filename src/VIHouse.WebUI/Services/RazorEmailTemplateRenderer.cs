using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;

namespace VIHouse.WebUI.Services;

/// <summary>
/// Renders Views/Emails/{templateKey}.cshtml to an HTML string outside of any real HTTP request —
/// the standard ASP.NET Core "render a Razor view to string" pattern, needed here because emails are
/// composed from background/service code (ApplicationService, PaymentService), not a controller action.
/// </summary>
public class RazorEmailTemplateRenderer(
    IRazorViewEngine viewEngine,
    ITempDataProvider tempDataProvider,
    IServiceProvider serviceProvider) : IEmailTemplateRenderer
{
    public async Task<RenderedEmail> RenderAsync<TModel>(string templateKey, TModel model, string culture, CancellationToken ct = default)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext { RequestServices = serviceProvider },
            new RouteData(),
            new ActionDescriptor());

        var viewPath = $"~/Views/Emails/{templateKey}.cshtml";
        var viewResult = viewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: true);
        if (!viewResult.Success)
            throw new InvalidOperationException($"Email template not found: {viewPath}");

        await using var writer = new StringWriter();
        var viewData = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        viewData["Lang"] = SiteCultures.Describe(culture).UrlCode;
        var viewContext = new ViewContext(
            actionContext, viewResult.View, viewData,
            new TempDataDictionary(actionContext.HttpContext, tempDataProvider),
            writer, new HtmlHelperOptions());

        // Emails are composed outside any HTTP request, so nothing sets the thread's ambient
        // culture the way UseRequestLocalization does for a page — it has to be forced on here,
        // for exactly the duration of the render, and restored afterward. OutboxProcessor reuses
        // this same thread across many queued messages in one batch, so leaking culture between
        // renders would silently mislanguage the next email in the batch.
        var resolved = new CultureInfo(SiteCultures.Normalise(culture));
        var (previousCulture, previousUiCulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = resolved;
            CultureInfo.CurrentUICulture = resolved;
            await viewResult.View.RenderAsync(viewContext);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }

        // Read back from the context, not from viewData: the page gets its own copy-on-write
        // ViewData (RazorPageActivator swaps it into the context), so whatever the template set is
        // only visible there.
        return new RenderedEmail(writer.ToString(), viewContext.ViewData["Subject"] as string);
    }
}
