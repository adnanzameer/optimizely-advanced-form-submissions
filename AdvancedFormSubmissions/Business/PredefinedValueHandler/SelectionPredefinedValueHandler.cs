using AdvancedFormSubmissions.Models;
using EPiServer;
using EPiServer.Forms.Core;
using EPiServer.Forms.EditView.Models.Internal;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.ServiceLocation;
using System;
using System.Globalization;
using System.Linq;

namespace AdvancedFormSubmissions.Business.PredefinedValueHandler;

[ServiceConfiguration(ServiceType = typeof(IFormPredefinedValueHandler))]
public class SelectionPredefinedValueHandler : IFormPredefinedValueHandler
{
    public int Priority => 100;

    public bool CanHandle(ElementBlockBase element)
    {
        return element is SelectionElementBlock;
    }

    public void Clear(ElementBlockBase element)
    {
        var sel = (SelectionElementBlock)element;

        if (sel.Items != null)
            foreach (var item in sel.Items)
                item.Checked = false;

        var contentLoader = ServiceLocator.Current.GetInstance<IContentLoader>();
        contentLoader.TryGet(element.Content.ContentGuid, new CultureInfo(element.FormElement.Form.Language), out SelectionElementBlock elementBlock);
        sel.PredefinedValue = elementBlock != null ? elementBlock.PredefinedValue : string.Empty;
        sel.PlaceHolder = elementBlock != null ? elementBlock.PlaceHolder : string.Empty;

    }

    public void SetValue(ElementBlockBase element, string value)
    {
        var sel = (SelectionElementBlock)element;

        if (sel.Items == null)
        {
            sel.PredefinedValue = value;
            return;
        }

        var selectedValues = value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        sel.Items = sel.Items
            .Select(i => new OptionItem
            {
                Caption = i.Caption,
                Value = i.Value,
                Checked = selectedValues.Contains(i.Value) ||
                          selectedValues.Contains(i.Caption)
            })
            .ToList();

        var selectedItem = sel.Items.FirstOrDefault(i => i.Checked == true);
        sel.PlaceHolder = selectedItem?.Caption;
        sel.PredefinedValue = value;
    }
}