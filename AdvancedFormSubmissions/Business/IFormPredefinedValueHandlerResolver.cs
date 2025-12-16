using AdvancedFormSubmissions.Models;
using EPiServer.Forms.Core;

namespace AdvancedFormSubmissions.Business;

public interface IFormPredefinedValueHandlerResolver
{
    IFormPredefinedValueHandler Resolve(ElementBlockBase element);
}