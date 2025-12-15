using System;
using System.Linq;
using EPiServer.Data.Dynamic;
using AdvancedFormSubmissions.Models;

namespace AdvancedFormSubmissions.Helpers
{
    public class FormSubmissionsSettingsRepository
    {
        private readonly DynamicDataStore _store = DynamicDataStoreFactory.Instance.GetStore(typeof(FormSubmissionsSettings))
                                                   ?? DynamicDataStoreFactory.Instance.CreateStore(typeof(FormSubmissionsSettings));

        public FormSubmissionsSettings Get(Guid formGuid, string language)
        {
            return _store.Items<FormSubmissionsSettings>()
                       .FirstOrDefault(s => s.FormGuid == formGuid && s.Language == language)
                   ?? new FormSubmissionsSettings { FormGuid = formGuid, Language = language };
        }

        public void Save(FormSubmissionsSettings settings)
        {
            settings.Updated = DateTime.UtcNow;
            _store.Save(settings);
        }

        public void Delete(Guid formGuid, string language)
        {
            var existing = Get(formGuid, language);
            if (existing != null)
                _store.Delete(existing.Id);
        }
    }
}