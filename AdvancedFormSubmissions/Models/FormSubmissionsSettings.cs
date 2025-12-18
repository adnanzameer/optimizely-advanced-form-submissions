using System;
using EPiServer.Data;
using EPiServer.Data.Dynamic;

namespace AdvancedFormSubmissions.Models;

[EPiServerDataStore(
    StoreName = "FormSubmissionsSettingsStore",
    AutomaticallyRemapStore = true)]
public class FormSubmissionsSettings
{
    public Identity Id { get; set; } = Identity.NewIdentity();
    public string UserId { get; set; }
    public Guid FormGuid { get; set; }
    public string Language { get; set; }
    public string HiddenColsJson { get; set; } = "{}";
    public string ColumnOrderJson { get; set; } = "[]";
    public DateTime Updated { get; set; } = DateTime.UtcNow;
}