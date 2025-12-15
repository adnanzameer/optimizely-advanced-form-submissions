using System.Collections.Generic;

namespace AdvancedFormSubmissions.Models;

public class DeleteSubmissionsRequest
{
    public string FormId { get; set; }
    public string Language { get; set; }
    public List<string> SubmissionIds { get; set; } = new();
}