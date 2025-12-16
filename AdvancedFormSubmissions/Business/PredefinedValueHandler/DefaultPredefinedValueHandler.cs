using AdvancedFormSubmissions.Models;
using EPiServer.Forms.Core;
using EPiServer.ServiceLocation;

namespace AdvancedFormSubmissions.Business.PredefinedValueHandler;

[ServiceConfiguration(ServiceType = typeof(IFormPredefinedValueHandler))]
public class DefaultPredefinedValueHandler : IFormPredefinedValueHandler
{
    public int Priority => -1000;

    public bool CanHandle(ElementBlockBase element) => true;

    public void Clear(ElementBlockBase element)
    {
        element.GetType()
            .GetProperty("PredefinedValue")
            ?.SetValue(element, string.Empty);
    }

    public void SetValue(ElementBlockBase element, string value)
    {
        element.GetType()
            .GetProperty("PredefinedValue")
            ?.SetValue(element, value);
    }
}