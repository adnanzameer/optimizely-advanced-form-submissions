using AdvancedFormSubmissions.Business;
using AdvancedFormSubmissions.Models;
using EPiServer.Core;
using EPiServer.Data.Dynamic;
using EPiServer.Forms.Core;
using EPiServer.Forms.Core.Data;
using EPiServer.Forms.Core.Models;
using EPiServer.Forms.Helpers.Internal;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.Forms.Implementation.Elements.BaseClasses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdvancedFormSubmissions.Helpers;

public static class FormHtmlHelperExtensions
{
    public delegate void FormElementsRenderer(
        IHtmlHelper html,
        int stepIndex,
        IEnumerable<IFormElement> elements);

    public static void RenderElementsHydrated(
        this IHtmlHelper html,
        int stepIndex,
        IEnumerable<IFormElement> elements,
        FormContainerBlock formContainerBlock,
        FormElementsRenderer renderer = null)
    {
        renderer ??= DefaultRenderer;

        var httpContext = html.ViewContext?.HttpContext;
        if (httpContext == null)
        {
            renderer(html, stepIndex, elements);
            return;
        }

        if (!UserHasAccess(httpContext).ConfigureAwait(false).GetAwaiter().GetResult())
        {
            renderer(html, stepIndex, elements);
            return;
        }

        var hydratedValues = GetHydratedValuesFromRequest(httpContext, formContainerBlock);
        var services = httpContext.RequestServices;
        var handlerResolver = services.GetRequiredService<IFormPredefinedValueHandlerResolver>();

        if (hydratedValues == null || !hydratedValues.Any())
        {
            var resetElements = elements
                .Select(e => ClearPredefinedValues(e, handlerResolver))
                .ToList();

            renderer(html, stepIndex, resetElements);
            return;
        }

        var hydratedElements = elements
            .Select(e => CloneAndHydrate(e, hydratedValues, handlerResolver))
            .ToList();

        renderer(html, stepIndex, hydratedElements);
    }


    private static void DefaultRenderer(
        IHtmlHelper html,
        int stepIndex,
        IEnumerable<IFormElement> elements)
    {
        html.RenderElementsInStep(stepIndex, elements);
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

        const string styleCss = "/_content/AdvancedFormSubmissions/styles/form.css";
        const string scriptJs = "/_content/AdvancedFormSubmissions/scripts/form.js";

        const string cssTag = $"<link rel=\"stylesheet\" href=\"{styleCss}\" />";
        var jsTag = $"<script src=\"{scriptJs}\" type=\"module\" data-form-id=\"{formId}\"></script>";

        return new HtmlString($"{cssTag}\n{jsTag}");
    }

    private static IFormElement CloneAndHydrate(IFormElement element, IDictionary<string, object> hydratedValues, IFormPredefinedValueHandlerResolver resolver)
    {
        if (element.SourceContent is not ElementBlockBase block)
            return element;

        if (block.CreateWritableClone() is not ElementBlockBase writable)
            return element;

        var value = GetHydratedValue(block, hydratedValues);
        if (!string.IsNullOrWhiteSpace(value))
            SetPredefinedValue(writable, value, resolver);

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

    private static IFormElement ClearPredefinedValues(
        IFormElement element,
        IFormPredefinedValueHandlerResolver resolver)
    {
        if (element.SourceContent is not ElementBlockBase block)
            return element;

        if (block is HiddenElementBlockBase)
            return element;

        if (block.CreateWritableClone() is not ElementBlockBase writable)
            return element;

        resolver.Resolve(writable)?.Clear(writable);

        element.SourceContent = writable as IContent;
        return element;
    }

    private static void SetPredefinedValue(
        ElementBlockBase writable,
        string value,
        IFormPredefinedValueHandlerResolver resolver)
    {
        if (writable is HiddenElementBlockBase)
            return;

        resolver.Resolve(writable)?.SetValue(writable, value);
    }

    private static async Task<bool> UserHasAccess(HttpContext httpContext)
    {
        var services = httpContext.RequestServices;
        var auth = services.GetRequiredService<IAuthorizationService>();
        var user = httpContext.User;
        var result = await auth.AuthorizeAsync(user, null, Constants.PolicyName);
        return result.Succeeded;
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
}