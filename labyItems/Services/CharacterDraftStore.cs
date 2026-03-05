using System;
using labyItems.Models.Characters;

namespace labyItems.Services;

public sealed class CharacterDraftStore : ICharacterDraftStore
{
    public CharacterDraft Draft { get; }

    public CharacterDraftStore(CharacterDraft draft)
    {
        Draft = draft ?? throw new ArgumentNullException(nameof(draft));
    }

    public void Save()
        => LiteDbService.UpsertDraft(Draft);
}
