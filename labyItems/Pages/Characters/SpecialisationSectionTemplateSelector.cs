using Microsoft.Maui.Controls;

namespace labyItems.Pages.Characters;

public sealed class SpecialisationSectionTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ChoiceTemplate { get; set; }
    public DataTemplate? MappedTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            SpecialisationGroupVm => ChoiceTemplate ?? MappedTemplate ?? new DataTemplate(),
            MappedSpecialisationSectionVm => MappedTemplate ?? ChoiceTemplate ?? new DataTemplate(),
            _ => ChoiceTemplate ?? MappedTemplate ?? new DataTemplate()
        };
    }
}
