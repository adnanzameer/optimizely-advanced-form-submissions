using System.Collections.Generic;

namespace AdvancedFormSubmissions.Models;

public class FormSettingsRequest
{
    public string FormId { get; set; }
    public string Language { get; set; }
    public Dictionary<string, bool> HiddenCols { get; set; }
    public List<string> ColumnOrder { get; set; }
    public string SortColumn { get; set; }
    public string SortDirection { get; set; }
}