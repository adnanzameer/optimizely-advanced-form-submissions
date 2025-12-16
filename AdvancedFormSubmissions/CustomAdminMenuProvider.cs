using System.Collections.Generic;
using AdvancedFormSubmissions.Models;
using EPiServer.Shell.Navigation;

namespace AdvancedFormSubmissions
{
    [MenuProvider]
    public class CustomAdminMenuProvider : IMenuProvider
    {
        public IEnumerable<MenuItem> GetMenuItems()
        {
            var link = new UrlMenuItem(
                "Form Submissions",
                MenuPaths.Global + "/cms/submissions",
                "/AdvancedFormSubmissions/Index")
            {
                SortIndex = 100,
                AuthorizationPolicy = Constants.PolicyName
            };

            return new List<MenuItem>
            {
                link
            };
        }
    }
}
