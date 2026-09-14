using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace VIHouse.WebUI.TagHelpers;

/// <summary>
/// Marks every <c>&lt;label asp-for="..."&gt;</c> bound to a field that must be filled in — a
/// <c>[Required]</c> property, or a non-nullable value type such as <c>DateTime</c>/<c>int</c> that
/// model binding cannot leave null — with a visible "required" indicator. This targets the same
/// element as the framework's built-in LabelTagHelper and runs after it (<see cref="Order"/>), so
/// admins can see at a glance which sections of a form are "not null" without opening every field.
/// Checkboxes are skipped: a bool always has a value, so a required marker on it is meaningless.
/// </summary>
[HtmlTargetElement("label", Attributes = ForAttributeName)]
public class RequiredLabelTagHelper : TagHelper
{
    private const string ForAttributeName = "asp-for";

    // The built-in LabelTagHelper runs at the default order (0); running after it means the
    // label's text content has already been written by the time we append the marker.
    public override int Order => 10;

    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression? For { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (For is null)
        {
            return;
        }

        var metadata = For.Metadata;
        var modelType = Nullable.GetUnderlyingType(metadata.ModelType) ?? metadata.ModelType;
        var isCheckable = modelType == typeof(bool);

        if (!metadata.IsRequired || isCheckable)
        {
            return;
        }

        output.PostContent.AppendHtml(
            "<span class=\"admin-required\" aria-hidden=\"true\">*</span>" +
            "<span class=\"visually-hidden\"> (required)</span>");
    }
}
