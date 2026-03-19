using labyItems.Helpers;
using labyItems.Models;

namespace labyItems.Pages.ItemCard;

public partial class ItemDetailCardPage : ContentPage
{
    public ItemDetailCardPage()
    {
        InitializeComponent();
    }

    public ItemDetailCardPage(Item item)
        : this()
    {
        ItemDetails.Item = item;
        Title = ItemDisplayHelper.BuildDisplayName(item);
    }
}
