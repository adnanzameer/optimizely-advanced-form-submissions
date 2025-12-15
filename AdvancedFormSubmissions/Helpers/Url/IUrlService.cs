using System.Globalization;
using EPiServer.Core;

namespace AdvancedFormSubmissions.Helpers.Url;

public interface IUrlService
{
    string ContentExternalUrl(ContentReference contentLink, CultureInfo contentLanguage);
}