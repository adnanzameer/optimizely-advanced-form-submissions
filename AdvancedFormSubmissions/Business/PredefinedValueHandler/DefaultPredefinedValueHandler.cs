using AdvancedFormSubmissions.Models;
using EPiServer;
using EPiServer.Forms.Core;
using EPiServer.Forms.Helpers.Internal;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.ServiceLocation;
using System.Globalization;

namespace AdvancedFormSubmissions.Business.PredefinedValueHandler;

[ServiceConfiguration(ServiceType = typeof(IFormPredefinedValueHandler))]
public class DefaultPredefinedValueHandler : IFormPredefinedValueHandler
{
    public int Priority => -1000;

    public bool CanHandle(ElementBlockBase element)
    {
        return true;
    }

    public void Clear(ElementBlockBase element)
    {
        element.GetType()
            .GetProperty("PredefinedValue")
            ?.SetValue(element, string.Empty);

        var contentLoader = ServiceLocator.Current.GetInstance<IContentLoader>();
        contentLoader.TryGet(element.Content.ContentGuid, new CultureInfo(element.FormElement.Form.Language), out ElementBlockBase elementBlock);

        var value =  elementBlock?.GetType()?.GetPropertyValue("PredefinedValue")?.ToString() ?? string.Empty;

        element.GetType().GetProperty("PredefinedValue")?.SetValue(element, value);
    }

    public void SetValue(ElementBlockBase element, string value)
    {
        element.GetType()
            .GetProperty("PredefinedValue")
            ?.SetValue(element, value);
    }
}