using AdvancedFormSubmissions.Controllers;
using AdvancedFormSubmissions.Models;
using EPiServer.Core;
using EPiServer.Data.Dynamic;
using EPiServer.Forms.Core;
using EPiServer.Forms.Core.Data;
using EPiServer.Forms.Core.Models;
using EPiServer.Forms.EditView.Models.Internal;
using EPiServer.Forms.Helpers.Internal;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.Shell;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AdvancedFormSubmissions.Helpers;

public static class FormHtmlHelperExtensions
{
    /// <summary>
    /// Hydrates and renders all form elements in a given step.
    /// Only CmsAdmins / WebAdmins / Administrators see hydrated read-only values.
    /// </summary>
    public static void RenderElementsHydrated(
        this IHtmlHelper html,
        int stepIndex,
        IEnumerable<IFormElement> elements,
    FormContainerBlock formContainerBlock)
    {
        var httpContext = html.ViewContext?.HttpContext;
        if (httpContext == null)
        {
            html.RenderElementsInStep(stepIndex, elements);
            return;
        }

        // Restrict hydration to admin roles only
        if (!UserHasAdminPolicy(httpContext).ConfigureAwait(false).GetAwaiter().GetResult())
        {
            html.RenderElementsInStep(stepIndex, elements);
            return;
        }

        // Get hydrated values if a submissionId exists
        var hydratedValues = GetHydratedValuesFromRequest(httpContext, formContainerBlock);

        // If no submissionId or no values, reset all predefined fields to blank
        if (hydratedValues == null || !hydratedValues.Any())
        {
            var resetElements = elements
                .Select(ClearPredefinedValues)
                .ToList();

            html.RenderElementsInStep(stepIndex, resetElements);
            return;
        }

        // Clone and inject hydrated values
        var writableElements = elements
            .Select(e => CloneAndHydrate(e, hydratedValues))
            .ToList();

        html.RenderElementsInStep(stepIndex, writableElements);
    }

    public static IHtmlContent RenderHydratedFormAssets(this IHtmlHelper html, FormContainerBlock form)
    {
        if (!html.ShouldDisableForm(form))
            return HtmlString.Empty;

        // Get the form DOM ID from the model (safe for client-side)
        var formId = form?.Form?.FormGuid.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(formId))
            return HtmlString.Empty;

        // Build link + script tags

        var styleCss = Paths.ToClientResource(typeof(FormSubmissionsController), "ClientResources/styles/form.css");
        var scriptJs = Paths.ToClientResource(typeof(FormSubmissionsController), "ClientResources/styles/form.js");

        var cssTag = $"<link rel=\"stylesheet\" href=\"{styleCss}\" />";
        var jsTag = $"<script src=\"{scriptJs}\" type=\"module\" data-form-id=\"{formId}\"></script>";

        return new HtmlString($"{cssTag}\n{jsTag}");
    }

    private static IFormElement CloneAndHydrate(IFormElement element, IDictionary<string, object> hydratedValues)
    {
        if (element.SourceContent is not ElementBlockBase block)
            return element;

        if (block.CreateWritableClone() is not ElementBlockBase writable)
            return element;

        var value = GetHydratedValue(block, hydratedValues);
        if (!string.IsNullOrWhiteSpace(value))
            SetPredefinedValue(writable, value);

        element.SourceContent = writable as IContent;
        return element;
    }

    private static bool ShouldDisableForm(this IHtmlHelper html, FormContainerBlock form)
    {
        var httpContext = html.ViewContext?.HttpContext;
        if (httpContext == null)
            return false;

        var request = httpContext.Request;
        var submissionId = request.Query["submissionId"].ToString();
        if (string.IsNullOrWhiteSpace(submissionId))
            return false;

        var lang = request.Query["language"].ToString();
        if (string.IsNullOrWhiteSpace(lang))
            lang = FormsExtensions.GetCurrentFormLanguage(form);

        var bag = LoadSubmissionBag(httpContext, form.Form.FormGuid, submissionId, lang);
        return bag != null && bag.Any();
    }

    private static IDictionary<string, object> GetHydratedValuesFromRequest(
        HttpContext httpContext,
        FormContainerBlock formContainerBlock)
    {
        var request = httpContext.Request;
        var submissionId = request.Query["submissionId"].ToString();
        var lang = request.Query["language"].ToString();

        if (string.IsNullOrWhiteSpace(lang))
            lang = FormsExtensions.GetCurrentFormLanguage(formContainerBlock);

        // If submissionId is missing, return null to trigger reset mode
        if (string.IsNullOrWhiteSpace(submissionId))
            return null;

        return GetHydratedValues(httpContext, formContainerBlock.Form.FormGuid, submissionId, lang);
    }

    /// <summary>
    /// Loads a submission property bag from storage for the specified form identity.
    /// </summary>
    private static PropertyBag LoadSubmissionBag(
        HttpContext httpContext,
        Guid formGuid,
        string submissionId,
        string language)
    {
        try
        {
            var services = httpContext.RequestServices;
            var submissionStorage = services.GetRequiredService<ISubmissionStorage>();

            var identity = new FormIdentity(formGuid, language);
            var bag = submissionStorage.LoadSubmissionFromStorage(identity, [submissionId])?.FirstOrDefault();

            return bag;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FormHydration] Failed to load submission bag: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Transforms a submission property bag into a dictionary of friendly hydrated values.
    /// </summary>
    private static IDictionary<string, object> GetHydratedValues(
        HttpContext httpContext,
        Guid formGuid,
        string submissionId,
        string language)
    {
        try
        {
            var bag = LoadSubmissionBag(httpContext, formGuid, submissionId, language);
            if (bag == null)
                return null;

            var services = httpContext.RequestServices;
            var formRepository = services.GetRequiredService<IFormRepository>();
            var formDataRepository = services.GetRequiredService<IFormDataRepository>();

            var identity = new FormIdentity(formGuid, language);
            var friendlyInfos = formRepository.GetDataFriendlyNameInfos(identity);

            var transformed = formDataRepository.TransformSubmissionDataWithFriendlyName(bag, friendlyInfos, true);

            return transformed as IDictionary<string, object>
                   ?? transformed.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FormHydration] Failed to transform submission data: {ex.Message}");
            return null;
        }
    }

    private static string GetHydratedValue(ElementBlockBase elementBlock, IDictionary<string, object> hydrated)
    {
        if (!string.IsNullOrWhiteSpace(elementBlock.Content.Name)
            && hydrated.TryGetValue(elementBlock.Content.Name, out var v1))
            return v1?.ToString();

        if (!string.IsNullOrWhiteSpace(elementBlock.FormElement.ElementName)
            && hydrated.TryGetValue(elementBlock.FormElement.ElementName, out var v2))
            return v2?.ToString();

        return null;
    }

    private static IFormElement ClearPredefinedValues(IFormElement element)
    {
        if (element.SourceContent is not ElementBlockBase block)
            return element;

        if (block.CreateWritableClone() is not ElementBlockBase writable)
            return element;

        // Reset predefined value
        var prop = writable.GetType().GetProperty("PredefinedValue");
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(writable, string.Empty);
        }

        // Reset selection or uploaded file state
        switch (writable)
        {
            case SelectionElementBlock sel:
                sel.Items = sel.Items?.Select(i =>
                {
                    i.Checked = false;
                    return i;
                }).ToList();
                sel.PlaceHolder = "";
                break;
            case FileUploadElementBlock file:
                file.Description = string.Empty;
                break;
        }

        element.SourceContent = writable as IContent;
        return element;
    }

    private static void SetPredefinedValue(ElementBlockBase writable, string value)
    {
        switch (writable)
        {
            case TextboxElementBlock tb:
                tb.PredefinedValue = value;
                break;
            case SelectionElementBlock sel:
                if (sel.Items != null && sel.Items.Any())
                {
                    var selectedValues = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var updatedItems = sel.Items
                        .Select(item => new OptionItem()
                        {
                            Caption = item.Caption,
                            Value = item.Value,
                            Checked = selectedValues.Contains(item.Value)
                                      || selectedValues.Contains(item.Caption)
                        })
                        .ToList();
                    var selectedItem = updatedItems.FirstOrDefault(x => x.Checked.HasValue && x.Checked.Value);
                    sel.PlaceHolder = selectedItem != null ? selectedItem.Caption : value;

                    sel.Items = updatedItems;
                }

                sel.PredefinedValue = value;
                break;
            case NumberElementBlock num:
                num.PredefinedValue = value;
                break;
            case FileUploadElementBlock file:
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var entries = value
                        .Split('|', StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim())
                        .ToList();

                    var linkList = new List<string>();

                    foreach (var entry in entries)
                    {
                        // Skip invalid entries
                        if (!entry.Contains("/"))
                        {
                            linkList.Add(System.Net.WebUtility.HtmlEncode(entry));
                            continue;
                        }

                        // Split at first #@
                        var idx = entry.IndexOf("#@", StringComparison.Ordinal);
                        string urlPart;
                        string namePart;

                        if (idx >= 0)
                        {
                            urlPart = entry[..idx].Trim();
                            namePart = entry[(idx + 2)..].Trim();
                        }
                        else
                        {
                            // No #@, fallback to filename
                            urlPart = entry.Trim();
                            namePart = Path.GetFileName(urlPart);
                        }

                        // Clean trailing ] or whitespaces
                        urlPart = urlPart.TrimEnd(' ', ']');
                        namePart = namePart.TrimEnd(' ', ']');

                        var safeUrl = System.Net.WebUtility.HtmlEncode(urlPart);
                        var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(namePart)
                            ? Path.GetFileName(urlPart)
                            : namePart);

                        linkList.Add($"<a href=\"{safeUrl}\" target=\"_blank\" rel=\"noopener\">{safeName}</a>");
                    }

                    // Line-break output
                    file.Description = string.Join("<br>", linkList);

                    file.GetType().GetProperty("Attributes")?.SetValue(
                        file,
                        new Dictionary<string, string> { { "class", "hydrated-file" } }
                    );
                }
                break;
            case UrlElementBlock url:
                url.PredefinedValue = value;
                break;
            default:
                var prop = writable.GetType().GetProperty("PredefinedValue");
                if (prop != null && prop.CanWrite)
                    prop.SetValue(writable, value);
                break;
        }
    }

    private static async Task<bool> UserHasAdminPolicy(HttpContext httpContext)
    {
        var services = httpContext.RequestServices;
        var auth = services.GetRequiredService<IAuthorizationService>();
        var user = httpContext.User;
        var result = await auth.AuthorizeAsync(user, null, Constants.PolicyName);
        return result.Succeeded;
    }
}
