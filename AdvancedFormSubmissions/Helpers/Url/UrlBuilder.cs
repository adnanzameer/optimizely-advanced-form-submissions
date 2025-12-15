using System;
using System.Globalization;
using System.Linq;
using EPiServer;
using EPiServer.Core;
using EPiServer.Web;
using EPiServer.Web.Routing;

namespace AdvancedFormSubmissions.Helpers.Url;

public class UrlBuilder(
    IUrlResolver urlResolver,
    ISiteDefinitionResolver siteDefinitionResolver,
    IContentRepository contentRepository,
    RoutingOptions routingOptions) : IUrlService
{
    public string ContentExternalUrl(ContentReference contentLink, CultureInfo contentLanguage)
    {
        var result = "";

        LoaderOptions loadingOptions = contentLanguage != null ? [LanguageLoaderOption.FallbackWithMaster(contentLanguage)] : [LanguageLoaderOption.FallbackWithMaster()];

        var pageData = contentRepository.Get<PageData>(contentLink, loadingOptions);

        if (!string.IsNullOrEmpty(pageData.ExternalURL))
        {
            result = pageData.ExternalURL;
        }

        if (string.IsNullOrEmpty(result))
        {
            var pageUrl = urlResolver.GetUrl(
                pageData.ContentLink,
                contentLanguage?.Name,
                new VirtualPathArguments
                {
                    ContextMode = ContextMode.Default,
                    ForceCanonical = true
                });

            result = pageUrl;
        }

        if (!Uri.TryCreate(result, UriKind.RelativeOrAbsolute, out var relativeUri))
            return ApplyTrailingSlash(result);

        if (relativeUri.IsAbsoluteUri)
            return ApplyTrailingSlash(result);

        var hostLanguage = string.Empty;

        var siteDefinition = siteDefinitionResolver.GetByContent(pageData.ContentLink, true, true);

        var hosts = siteDefinition.GetHosts(contentLanguage, true).ToList();

        var host = hosts.FirstOrDefault(h => h.Language != null && h.Language.Equals(contentLanguage));

        host ??= hosts.FirstOrDefault(h => h.Type == HostDefinitionType.Primary);

        var baseUri = siteDefinition.SiteUrl;

        if (host != null && host.Name.Equals("*") == false)
        {
            Uri.TryCreate(siteDefinition.SiteUrl.Scheme + "://" + host.Name, UriKind.Absolute, out baseUri);

            if (host.Language != null)
            {
                hostLanguage = "/" + host.Language.Name.ToLower() + "/";
            }
        }

        if (baseUri == null)
        {
            Uri.TryCreate(SiteDefinition.Current.SiteUrl.AbsoluteUri, UriKind.Absolute, out baseUri);
        }

        if (baseUri != null)
        {
            var absoluteUri = new Uri(baseUri, relativeUri);
            if (!string.IsNullOrEmpty(hostLanguage))
            {
                var absoluteUrl = absoluteUri.AbsoluteUri.Replace(hostLanguage, "/");
                return ApplyTrailingSlash(absoluteUrl);
            }
            return ApplyTrailingSlash(absoluteUri.AbsoluteUri);
        }
        return string.Empty;
    }

    private string ApplyTrailingSlash(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return routingOptions.UseTrailingSlash ? "/" : string.Empty;
        }

        if (routingOptions.UseTrailingSlash && !url.EndsWith("/"))
        {
            url += "/";
        }
        else if (!routingOptions.UseTrailingSlash && url.EndsWith("/"))
        {
            url = url.TrimEnd('/');
        }

        return url.ToLower();
    }
}