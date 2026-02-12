using System.Globalization;
using AdvancedFormSubmissions.Models;
using EPiServer;
using EPiServer.Forms.Core;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.ServiceLocation;

namespace AdvancedFormSubmissions.Business.PredefinedValueHandler;

[ServiceConfiguration(ServiceType = typeof(IFormPredefinedValueHandler))]
public class TextboxPredefinedValueHandler : IFormPredefinedValueHandler
{
    public int Priority => 50;

    public bool CanHandle(ElementBlockBase element)
    {
        return element is TextboxElementBlock;
    }

    public void Clear(ElementBlockBase element)
    {
        var contentLoader = ServiceLocator.Current.GetInstance<IContentLoader>();
        contentLoader.TryGet(element.Content.ContentGuid, new CultureInfo(element.FormElement.Form.Language), out TextboxElementBlock elementBlock);
        ((TextboxElementBlock)element).PredefinedValue = elementBlock != null ? elementBlock.PredefinedValue : string.Empty;
    }

    public void SetValue(ElementBlockBase element, string value)
    {
        ((TextboxElementBlock)element).PredefinedValue = value;
    }
}
