using System.Windows.Input;
using labyItems.Infrastructure;
using labyItems.Models.Characters;
using labyItems.Services;

public partial class CharacterViewModel : ObservableObject
{
    private readonly IBattleboardExportService _exporter;

    public CharacterDraft Draft { get; } = new();
    public ICommand ExportToBattleboardCommand { get; }
    public CharacterViewModel(IBattleboardExportService exporter)
    {
        _exporter = exporter;
        ExportToBattleboardCommand = new Command(async () =>
        {
            var path = await _exporter.ExportAsync(Draft);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"{Draft.Name}'s battleboard",
                File = new ShareFile(path)
            });
        });
    }
}
