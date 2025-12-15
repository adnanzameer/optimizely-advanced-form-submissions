using EPiServer.Forms.Implementation.Elements;
using EPiServer.ServiceLocation;
using EPiServer.Shell;

namespace AdvancedFormSubmissions;

[ServiceConfiguration(typeof(ViewConfiguration))]
public class AdvancedFormSubmissionsViewConfiguration : ViewConfiguration<FormContainerBlock>
{
    public AdvancedFormSubmissionsViewConfiguration()
    {
        Key = "advancedformsubmissions";
        Name = "Advanced Form Submissions";
        Description = "Opens the enhanced form submissions dashboard.";
        IconClass = "epi-iconSharedBlock";
        ControllerType = "epi-cms/widget/IFrameController";
        ViewType = "/FormSubmissions/index";
        Category = "content";
        SortOrder = 300;
    }
}