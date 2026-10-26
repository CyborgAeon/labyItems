using labyItems.Models;
using labyItems.Pages;
using labyItems.Services;
using labyItems.Helpers;
using ClassCardPage = labyItems.Pages.ClassCard.ClassCard;
using RaceCardPage = labyItems.Pages.RaceCard.RaceCard;

namespace labyItems.Pages.Characters;

public partial class CharacterBuilder : ContentView
{
    public CharacterBuilder()
    {
        InitializeComponent();
    }

    public CharacterBuilder(CharacterBuilderVm vm) : this()
    {
        BindingContext = vm;
    }

    private async void OnClassInfoTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not ClassCardVm item)
            return;

        try
        {
            var classes = await ClassService.GetAllAsync();
            if (!classes.TryGetValue(item.Key, out var record))
                return;

            await Navigation.PushAsync(new ClassCardPage(item.Name, record));
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("CHARACTER_BUILDER_CLASS_DETAILS_FAILED", "Failed to open class details.", ex);
        }
    }

    private async void OnRaceInfoTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not RaceCardVm item)
            return;

        try
        {
            var races = await PeopleService.GetAllAsync();
            if (!races.TryGetValue(item.Name, out var record))
                return;

            await Navigation.PushAsync(new RaceCardPage(item.Name, record));
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("CHARACTER_BUILDER_RACE_DETAILS_FAILED", "Failed to open race details.", ex);
        }
    }
}
