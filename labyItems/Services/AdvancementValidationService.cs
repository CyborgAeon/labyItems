namespace labyItems.Services;

public sealed class AdvancementValidationService : IAdvancementValidationService
{
    public bool CanSave(bool hasMiracleAlignmentIssues, bool showEvilStairway, bool hasEvilStairwayValidationError)
        => !hasMiracleAlignmentIssues && (!showEvilStairway || !hasEvilStairwayValidationError);
}
