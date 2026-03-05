using labyItems.Models.Characters;

namespace labyItems.Services;

public interface ICharacterDraftStore
{
    CharacterDraft Draft { get; }
    void Save();
}
