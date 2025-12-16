using System.Linq;
using AdvancedFormSubmissions.Models;
using EPiServer.Forms.Core;
using EPiServer.ServiceLocation;
using System.Collections.Generic;

namespace AdvancedFormSubmissions.Business;


[ServiceConfiguration(ServiceType = typeof(IFormPredefinedValueHandlerResolver))]
public class FormPredefinedValueHandlerResolver(IEnumerable<IFormPredefinedValueHandler> handlers)
    : IFormPredefinedValueHandlerResolver
{
    public IFormPredefinedValueHandler Resolve(ElementBlockBase element)
    {
        return handlers
            .Where(h => h.CanHandle(element))
            .OrderByDescending(h => h.Priority)
            .FirstOrDefault();
    }
}