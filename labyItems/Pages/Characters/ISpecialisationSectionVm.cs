using System.ComponentModel;
using System.Windows.Input;

namespace labyItems.Pages.Characters;

public enum SpecialisationSectionType
{
    Choice,
    Mapped,
    RaceSubtype
}

public interface ISpecialisationSectionVm : INotifyPropertyChanged
{
    string SectionId { get; }
    string DetailKey { get; }
    string Title { get; }
    string StatusText { get; }
    string CardStateText { get; }
    string DisplaySubtitle { get; }
    bool IsComplete { get; }
    bool IsVisible { get; }
    bool IsExpanded { get; set; }
    ICommand ToggleExpandedCommand { get; }
    SpecialisationSectionType SectionType { get; }
}
