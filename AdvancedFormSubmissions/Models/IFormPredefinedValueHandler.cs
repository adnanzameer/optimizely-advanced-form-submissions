using EPiServer.Forms.Core;

namespace AdvancedFormSubmissions.Models;

public interface IFormPredefinedValueHandler
{
    int Priority { get; }

    bool CanHandle(ElementBlockBase element);

    void Clear(ElementBlockBase element);

    void SetValue(ElementBlockBase element, string value);
}