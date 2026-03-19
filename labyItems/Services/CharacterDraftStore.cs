using System;
using labyItems.Helpers;
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
    {
        try
        {
            LiteDbService.UpsertDraft(Draft);
        }
        catch (Exception ex)
        {
            RuntimeLog.Write("CHAR_DRAFT_SAVE", "Failed while saving character draft.", ex);
            throw;
        }
    }
}
