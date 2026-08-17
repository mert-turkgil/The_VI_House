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
    public async Task<string> RenderAsync<TModel>(string templateKey, TModel model, CancellationToken ct = default)
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
        var viewContext = new ViewContext(
            actionContext, viewResult.View, viewData,
            new TempDataDictionary(actionContext.HttpContext, tempDataProvider),
            writer, new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
