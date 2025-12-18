using System;
using System.Linq;
using AdvancedFormSubmissions.Models;
using EPiServer.Data.Dynamic;

namespace AdvancedFormSubmissions.Helpers;

public class FormSubmissionsSettingsRepository
{
    private readonly DynamicDataStore _store = DynamicDataStoreFactory.Instance.GetStore(typeof(FormSubmissionsSettings))
                                               ?? DynamicDataStoreFactory.Instance.CreateStore(typeof(FormSubmissionsSettings));

    public FormSubmissionsSettings Get(
        Guid formGuid,
        string language,
        string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var userSettings = _store.Items<FormSubmissionsSettings>()
                .FirstOrDefault(x =>
                    x.FormGuid == formGuid &&
                    x.Language == language &&
                    x.UserId == userId);

            if (userSettings != null)
                return userSettings;
        }

        return new FormSubmissionsSettings
        {
            FormGuid = formGuid,
            Language = language,
            UserId = userId
        };
    }

    public void Save(FormSubmissionsSettings settings)
    {
        settings.Updated = DateTime.UtcNow;
        _store.Save(settings);
    }

    public void Delete(
        Guid formGuid,
        string language,
        string userId)
    {
        var items = _store.Items<FormSubmissionsSettings>()
            .Where(x =>
                x.FormGuid == formGuid &&
                x.Language == language &&
                x.UserId == userId)
            .ToList();

        foreach (var item in items)
            _store.Delete(item.Id);
    }
}