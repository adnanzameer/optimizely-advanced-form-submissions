using AdvancedFormSubmissions.Models;
using EPiServer.Forms.Core;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.ServiceLocation;

namespace AdvancedFormSubmissions.Business.PredefinedValueHandler;

[ServiceConfiguration(ServiceType = typeof(IFormPredefinedValueHandler))]
public class NumberPredefinedValueHandler : IFormPredefinedValueHandler
{
    public int Priority => 50;

    public bool CanHandle(ElementBlockBase element)
        => element is NumberElementBlock;

    public void Clear(ElementBlockBase element)
    {
        ((NumberElementBlock)element).PredefinedValue = string.Empty;
    }

    public void SetValue(ElementBlockBase element, string value)
    {
        ((NumberElementBlock)element).PredefinedValue = value;
    }
}