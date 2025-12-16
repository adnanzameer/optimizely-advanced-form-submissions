using System;
using AdvancedFormSubmissions.Helpers.Url;
using AdvancedFormSubmissions.Models;
using EPiServer.Core.Internal;
using EPiServer.Data.Dynamic;
using EPiServer.Forms.Core;
using EPiServer.Forms.Core.Data;
using EPiServer.Forms.Core.Models;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.Security;
using EPiServer.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using AdvancedFormSubmissions.Helpers;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Forms.Core.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AdvancedFormSubmissions.Controllers;

[Authorize(Policy = Constants.PolicyName)]
[Route("[controller]")]
public class AdvancedFormSubmissionsController(
    ISubmissionStorage submissionStorage,
    IFormRepository formRepository,
    IContentRepository contentLoader,
    ILanguageBranchRepository languageBranchRepository,
    IUrlService urlService,
    IFormDataRepository formDataRepository,
    IConfiguration configuration,
    ISiteDefinitionRepository siteDefinitionRepository,
    ISiteDefinitionResolver siteDefinitionResolver,
    IContentSecurityRepository contentSecurityRepository,
    IOptions<FormsConfigOptions> formsConfigOptions)
    : Controller
{
    [Route("[action]")]
    public IActionResult Index(string id)
    {
        FormContainerBlock form = null;

        try
        {
            ContentReference contentRef = null;

            if (!string.IsNullOrWhiteSpace(id))
            {
                if (id.Contains("_"))
                {
                    var parts = id.Split('_');
                    if (int.TryParse(parts[0], out var cid))
                    {
                        int? workId = int.TryParse(parts.ElementAtOrDefault(1), out var wid) ? wid : null;
                        contentRef = new ContentReference(cid, workId ?? 0);
                    }
                }
                else if (int.TryParse(id, out var cid))
                {
                    contentRef = new ContentReference(cid);
                }
            }

            if (contentRef != null && !ContentReference.IsNullOrEmpty(contentRef))
                if (contentLoader.TryGet(contentRef, out FormContainerBlock block))
                    form = block;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FormSubmissionsController] Preselect form failed: {ex.Message}");
        }

        ViewBag.PreselectedFormGuid = form?.Form?.FormGuid.ToString("N");

        return View();
    }

    [HttpGet]
    [Route("[action]")]
    public JsonResult GetLanguages()
    {
        try
        {
            var langs = languageBranchRepository
                .ListEnabled()
                .Select(x => new { x.LanguageID, x.Name })
                .OrderBy(x => x.Name)
                .ToList();

            return Json(new { status = true, data = langs });
        }
        catch (Exception ex)
        {
            return Json(new { status = false, message = ex.Message });
        }
    }

    [Route("[action]")]
    public JsonResult GetForms(string language = "en", string site = "All")
    {
        try
        {
            var forms = GetAllFormsForSite(language, site)
                .Select(f => new
                {
                    id = f.Form.FormGuid.ToString("N"),
                    contentId = f.Content.ContentLink.ID,
                    name = string.IsNullOrWhiteSpace(f.Content.Name)
                        ? f.Form.FormGuid.ToString("N")
                        : f.Content.Name
                })
                .OrderBy(x => x.name)
                .ToList();

            return Json(new { status = true, data = forms });
        }
        catch (Exception ex)
        {
            return Json(new { status = false, message = ex.Message });
        }
    }

    [Route("[action]")]
    public JsonResult GetSubmissions(
        string formId,
        string language = "en",
        int page = 1,
        int pageSize = 50,
        DateTime? from = null,
        DateTime? to = null,
        string q = null)
    {
        try
        {
            if (string.IsNullOrEmpty(formId))
                return Json(new { status = false, message = "formId is required." });

            if (!Guid.TryParseExact(formId, "N", out var formGuid))
                return Json(new { status = false, message = "Invalid formId format." });

            var formIdentity = new FormIdentity(formGuid, language);
            var begin = from ?? DateTime.MinValue;
            var end = to ?? DateTime.MaxValue;

            // Load raw PropertyBag list
            var raw = submissionStorage
                .LoadSubmissionFromStorage(formIdentity, begin, end)
                ?.ToList() ?? [];

            // Extract SubmissionId from DDS record metadata if missing
            var propertyBags = new List<(string Id, PropertyBag Data)>();
            foreach (var bag in raw)
            {
                string idValue = null;

                // Try from known system column (sometimes present)
                if (bag.TryGetValue("SYSTEMCOLUMN_SubmissionId", out var sid) && sid != null)
                    idValue = sid.ToString();
                else if (bag.TryGetValue("SystemSubmissionId", out var sid2) && sid2 != null)
                    idValue = sid2.ToString();

                // If still missing, fall back to DDS item Id (the record’s actual Guid)
                if (string.IsNullOrEmpty(idValue) && bag is IDynamicData ddsRecord)
                    idValue = ddsRecord.Id.ToString();

                propertyBags.Add((idValue ?? Guid.NewGuid().ToString(), bag));
            }

            // Optional search
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                propertyBags = propertyBags
                    .Where(b => b.Data.Values.Any(v =>
                        v?.ToString()?.Contains(term, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();
            }

            var total = propertyBags.Count;
            var skip = (page - 1) * pageSize;

            var items = propertyBags
                .OrderByDescending(b => GetDateValue(b.Data, "SYSTEMCOLUMN_SubmitTime"))
                .Skip(skip)
                .Take(pageSize)
                .Select(b => new
                {
                    SubmissionId = b.Id,
                    Fields = BuildFriendlyFields(formIdentity, b.Data, language)
                });

            return Json(new { status = true, total, items });
        }
        catch (Exception ex)
        {
            return Json(new { status = false, message = ex.Message });
        }
    }

    [Route("[action]")]
    public JsonResult GetSubmissionDetail(string submissionId, string formId, string language = "en")
    {
        try
        {
            if (string.IsNullOrEmpty(formId) || string.IsNullOrEmpty(submissionId))
                return Json(new { status = false, message = "Both formId and submissionId are required." });

            if (!Guid.TryParseExact(formId, "N", out var formGuid))
                return Json(new { status = false, message = "Invalid formId format." });

            var formIdentity = new FormIdentity(formGuid, language);
            var bag = submissionStorage
                .LoadSubmissionFromStorage(formIdentity, [submissionId])
                ?.FirstOrDefault();

            if (bag == null)
                return Json(new { status = false, message = "Submission not found." });

            string sid = null;
            if (bag.TryGetValue("SYSTEMCOLUMN_SubmissionId", out var s1) && s1 != null)
                sid = s1.ToString();
            else if (bag is IDynamicData dds)
                sid = dds.Id.ToString();

            var data = new
            {
                SubmissionId = sid ?? submissionId,
                Fields = BuildFriendlyFields(formIdentity, bag, language)
            };

            return Json(new { status = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { status = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Route("[action]")]
    [ValidateAntiForgeryToken]
    public JsonResult DeleteSubmissions([FromBody] DeleteSubmissionsRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.FormId) || request.SubmissionIds == null || !request.SubmissionIds.Any())
                return Json(new { status = false, message = "Invalid request data." });

            if (!Guid.TryParseExact(request.FormId, "N", out var formGuid))
                return Json(new { status = false, message = "Invalid formId format." });

            var formIdentity = new FormIdentity(formGuid, request.Language);

            var count = 0;
            foreach (var sid in request.SubmissionIds)
                try
                {
                    submissionStorage.Delete(formIdentity, sid);
                    count++;
                }
                catch
                {
                    /* skip failed deletes */
                }

            return Json(new { status = true, deleted = count });
        }
        catch (Exception ex)
        {
            return Json(new { status = false, message = ex.Message });
        }
    }

    [Route("[action]")]
    public JsonResult GetFormSettings(string formId, string language = "en")
    {
        try
        {
            if (!Guid.TryParseExact(formId, "N", out var formGuid))
                return Json(new { status = false, message = "Invalid formId format." });

            var userId = GetCurrentUserId();
            var repo = new FormSubmissionsSettingsRepository();
            var settings = repo.Get(formGuid, language, userId);

            var hiddenCols = string.IsNullOrWhiteSpace(settings.HiddenColsJson)
                 ? new Dictionary<string, bool>()
                 : JsonSerializer.Deserialize<Dictionary<string, bool>>(settings.HiddenColsJson);

            var columnOrder = string.IsNullOrWhiteSpace(settings.ColumnOrderJson)
                 ? []
                 : JsonSerializer.Deserialize<List<string>>(settings.ColumnOrderJson);

            return Json(new
            {
                status = true,
                settings = new
                {
                    hiddenCols,
                    columnOrder
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new { status = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Route("[action]")]
    [ValidateAntiForgeryToken]
    public JsonResult SaveFormSettings([FromBody] FormSettingsRequest payload)
    {
        try
        {
            var formId = payload.FormId;
            var language = payload.Language;
            if (!Guid.TryParseExact(formId, "N", out var formGuid))
                return Json(new { status = false, message = "Invalid formId format." });

            var userId = GetCurrentUserId();
            var repo = new FormSubmissionsSettingsRepository();
            var settings = repo.Get(formGuid, language, userId);

            settings.HiddenColsJson = JsonSerializer.Serialize(payload.HiddenCols ?? new Dictionary<string, bool>());
            settings.ColumnOrderJson = JsonSerializer.Serialize(payload.ColumnOrder ?? []);

            repo.Save(settings);
            return Json(new { status = true });
        }
        catch (Exception ex)
        {
            return Json(new { status = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Route("[action]")]
    [ValidateAntiForgeryToken]
    public JsonResult ClearFormCache([FromBody] FormSettingsRequest payload)
    {
        try
        {
            var formId = payload.FormId;
            var language = payload.Language;
            if (!Guid.TryParseExact(formId, "N", out var formGuid))
                return Json(new { status = false, message = "Invalid formId format." });

            var userId = GetCurrentUserId();
            var repo = new FormSubmissionsSettingsRepository();
            repo.Delete(formGuid, language, userId);

            return Json(new { status = true, message = "Form cache cleared." });
        }
        catch (Exception ex)
        {
            return Json(new { status = false, message = ex.Message });
        }
    }

    [Route("[action]")]
    public JsonResult GetSites()
    {
        try
        {
            var sites = siteDefinitionRepository.List()
                .Select(s => new { id = s.Id.ToString("N"), displayName = s.Name })
                .OrderBy(s => s.displayName)
                .ToList();

            return Json(new { status = true, data = sites });
        }
        catch (Exception ex)
        {
            return Json(new { status = false, message = ex.Message });
        }
    }

    // -------------------------------------------------------------
    // Helper methods
    // -------------------------------------------------------------
    public IList<FormContainerBlock> GetAllForms(string language)
    {
        const string sql = """
                               SELECT c.pkID
                               FROM dbo.tblContent c
                               INNER JOIN dbo.tblContentType ct on c.fkContentTypeID = ct.pkID
                               WHERE c.Deleted = 0 and ct.Name like '%FormContainer%'
                               ORDER BY c.pkID
                           """;

        var ids = new List<int>();

        using (var conn = new SqlConnection(configuration.GetConnectionString("EPiServerDB")))
        using (var cmd = new SqlCommand(sql, conn))
        {
            conn.Open();
            using (var reader = cmd.ExecuteReader())
            {
                var idx = reader.GetOrdinal("pkID");

                while (reader.Read())
                    ids.Add(reader.GetInt32(idx));
            }
        }

        var results = new List<FormContainerBlock>();

        LoaderOptions loadingOptions = !string.IsNullOrEmpty(language)
            ? [LanguageLoaderOption.FallbackWithMaster(new CultureInfo(language))]
            : [LanguageLoaderOption.FallbackWithMaster()];

        var requiredLevel = GetMinAccessLevel();

        foreach (var link in ids.Select(id => new ContentReference(id)))
        {
            if (!contentLoader.TryGet<FormContainerBlock>(link, loadingOptions, out var form))
                continue;

            if (!UserHasSubmissionAccess(form, requiredLevel))
                continue;

            results.Add(form);
        }

        return results;
    }

    private bool UserHasSubmissionAccess(FormContainerBlock form, AccessLevel required)
    {
        var principal = HttpContext?.User;

        if (principal == null)
            return false;

        var descriptor = contentSecurityRepository.Get(form.Content.ContentLink);

        return descriptor.HasAccess(principal, required);
    }

    public AccessLevel GetMinAccessLevel()
    {
        if (formsConfigOptions != null)
        {
            return formsConfigOptions.Value.MinimumAccessRightLevelToReadFormData;
        }

        return AccessLevel.Edit;
    }

    private ExpandoObject BuildFriendlyFields(FormIdentity formIdentity, PropertyBag bag, string language)
    {
        var fields = new ExpandoObject();
        IDictionary<string, object> dict = fields;

        // 1 get ordered field names from the form
        var formFieldOrder = new List<string>();
        try
        {
            var formBlock = contentLoader.Get<FormContainerBlock>(formIdentity.Guid);
            if (formBlock?.ElementsArea != null)
            {
                foreach (var item in formBlock.ElementsArea.Items)
                {
                    if (item?.ContentLink == null)
                        continue;

                    if (contentLoader.TryGet<IContent>(item.ContentLink, out var element))
                        formFieldOrder.Add(element.Name);
                }
            }
        }
        catch
        {
            // ignore if form not found or deleted
        }

        // 2 filter out system fields and run through friendly name transformer
        var data = bag
            .Where(kv => !kv.Key.StartsWith("System", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var friendlyInfos = formRepository.GetDataFriendlyNameInfos(formIdentity).ToList();

        // assume this returns IEnumerable<KeyValuePair<string, object>>
        var transformed = formDataRepository
            .TransformSubmissionDataWithFriendlyName(data, friendlyInfos, true)
            .ToList();

        // 3 first, add values in exact form layout order
        var remaining = transformed.ToList();
        foreach (var fieldName in formFieldOrder)
        {
            // find matching fields for this layout name
            var matches = remaining
                .Where(x => string.Equals(x.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
                continue;

            foreach (var match in matches)
            {
                AddField(fieldName, match.Value, dict);

                remaining.RemoveAll(x =>
                    string.Equals(x.Key, match.Key, StringComparison.OrdinalIgnoreCase) &&
                    Equals(x.Value, match.Value)
                );
            }
        }

        // 4 then add any remaining user fields in the order returned from TransformSubmissionDataWithFriendlyName
        foreach (var item in remaining)
            AddField(item.Key, item.Value, dict);

        // 5 system fields

        // Submitted from
        var editUrl = string.Empty;
        if (bag.TryGetValue("SYSTEMCOLUMN_HostedPage", out var hostedValue) && hostedValue != null)
        {
            var hostedDisplay = hostedValue.ToString();


            if (ContentReference.TryParse(hostedDisplay, out var cref) &&
                contentLoader.TryGet<IContent>(cref, out var content))
            {
                hostedDisplay = content.Name;

                var externalUrl = urlService.ContentExternalUrl(
                    content.ContentLink,
                    new CultureInfo(language));

                var submissionId = string.Empty;

                if (bag.TryGetValue("SYSTEMCOLUMN_SubmissionId", out var sid) && sid != null)
                    submissionId = sid.ToString();
                else if (bag.TryGetValue("SystemSubmissionId", out var sid2) && sid2 != null)
                    submissionId = sid2.ToString();
                else if (bag is IDynamicData dds)
                    submissionId = dds.Id.ToString();

                if (!string.IsNullOrEmpty(externalUrl) && !string.IsNullOrEmpty(submissionId))
                {
                    var sep = externalUrl.Contains("?") ? "&" : "?";
                    editUrl =
                        $"{externalUrl}{sep}submissionId={Uri.EscapeDataString(submissionId)}&language={Uri.EscapeDataString(language)}";
                }
            }
            else if (Uri.TryCreate(hostedDisplay, UriKind.Absolute, out _))
            {
                editUrl = hostedDisplay;
            }

            AddField("Submitted from", new { name = hostedDisplay, link = editUrl }, dict);
        }

        // Time
        if (bag.TryGetValue("SYSTEMCOLUMN_SubmitTime", out var submitTime))
            AddField("Time", FormatDate(submitTime), dict);

        // By user
        if (bag.TryGetValue("SYSTEMCOLUMN_SubmitUser", out var submitUser))
            AddField("By user", submitUser?.ToString() ?? string.Empty, dict);

        // Finalized
        if (bag.TryGetValue("SYSTEMCOLUMN_FinalizedSubmission", out var finalized))
            AddField("Finalized", finalized?.ToString() ?? string.Empty, dict);

        if (!string.IsNullOrEmpty(editUrl))
            AddField("Open", editUrl, dict);

        return fields;
    }

    private static void AddField(string key, object value, IDictionary<string, object> dict)
    {
        if (!dict.ContainsKey(key))
        {
            dict[key] = value;
            return;
        }

        var index = 2;
        string newKey;
        do
        {
            newKey = $"{key}_{index}";
            index++;
        }
        while (dict.ContainsKey(newKey));

        dict[newKey] = value;
    }


    private static DateTime GetDateValue(PropertyBag bag, string key)
    {
        if (bag.TryGetValue(key, out var value)
            && DateTime.TryParse(value?.ToString(), out var dt))
            return dt;

        return DateTime.MinValue;
    }

    private static string FormatDate(object value)
    {
        if (value == null) return string.Empty;
        if (DateTime.TryParse(value.ToString(), out var dt))
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        return value.ToString() ?? string.Empty;
    }

    private IList<FormContainerBlock> GetAllFormsForSite(string language, string siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) || siteId.Equals("All", StringComparison.OrdinalIgnoreCase))
            return GetAllForms(language);

        if (!Guid.TryParse(siteId, out var siteGuid))
            return GetAllForms(language);

        var site = siteDefinitionRepository.Get(siteGuid);
        if (site == null)
            return GetAllForms(language);

        var results = new List<FormContainerBlock>();

        var allForms = GetAllForms(language);

        foreach (var form in allForms)
        {
            if (IsFormUsedBySiteViaReferences(form, site))
                results.Add(form);
        }

        return results;
    }

    private bool IsFormUsedBySiteViaReferences(FormContainerBlock form, SiteDefinition site)
    {
        try
        {
            var refs = contentLoader.GetReferencesToContent(form.Content.ContentLink, false);
            if (refs == null) return false;

            foreach (var r in refs)
            {
                var ownerRef = r.OwnerID;
                if (ContentReference.IsNullOrEmpty(ownerRef)) continue;

                if (!contentLoader.TryGet<IContent>(ownerRef, out var ownerContent))
                    continue;

                var ownerSite = siteDefinitionResolver.GetByContent(ownerContent.ContentLink, true);
                if (ownerSite == null) continue;

                if (ownerSite.Id == site.Id)
                    return true;
            }
        }
        catch
        {
            // ignore errors
        }

        return false;
    }

    private string GetCurrentUserId()
    {
        return User?.Identity?.Name;
    }
}