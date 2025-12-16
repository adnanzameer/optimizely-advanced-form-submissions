using AdvancedFormSubmissions.Models;
using EPiServer.Forms.Core;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.ServiceLocation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace AdvancedFormSubmissions.Business.PredefinedValueHandler;

[ServiceConfiguration(ServiceType = typeof(IFormPredefinedValueHandler))]
public class FileUploadPredefinedValueHandler : IFormPredefinedValueHandler
{
    public int Priority => 200;

    public bool CanHandle(ElementBlockBase element)
        => element is FileUploadElementBlock;

    public void Clear(ElementBlockBase element)
    {
        var file = (FileUploadElementBlock)element;
        file.Description = string.Empty;
    }

    public void SetValue(ElementBlockBase element, string value)
    {
        var file = (FileUploadElementBlock)element;

        if (string.IsNullOrWhiteSpace(value))
            return;

        var entries = value
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim());

        var links = new List<string>();

        foreach (var entry in entries)
        {
            var idx = entry.IndexOf("#@", StringComparison.Ordinal);
            string url;
            string name;

            if (idx >= 0)
            {
                url = entry[..idx].Trim();
                name = entry[(idx + 2)..].Trim();
            }
            else
            {
                url = entry;
                name = Path.GetFileName(entry);
            }

            url = url.TrimEnd(' ', ']');
            name = name.TrimEnd(' ', ']');

            var safeUrl = System.Net.WebUtility.HtmlEncode(url);
            var safeName = System.Net.WebUtility.HtmlEncode(
                string.IsNullOrWhiteSpace(name) ? Path.GetFileName(url) : name
            );

            links.Add($"<a href=\"{safeUrl}\" target=\"_blank\" rel=\"noopener\">{safeName}</a>");
        }

        file.Description = string.Join("<br>", links);

        file.GetType().GetProperty("Attributes")?.SetValue(
            file,
            new Dictionary<string, string> { { "class", "hydrated-file" } }
        );
    }
}