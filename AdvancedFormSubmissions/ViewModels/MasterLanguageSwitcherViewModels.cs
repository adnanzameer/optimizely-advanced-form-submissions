using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AdvancedFormSubmissions.ViewModels
{
    /// <summary>
    /// View model class for Orphaned Properties admin plugin
    /// </summary>
    public class MasterLanguageSwitcherViewModels
    {
        public List<SelectListItem> LanguageBranches { get; set; }
    }
}