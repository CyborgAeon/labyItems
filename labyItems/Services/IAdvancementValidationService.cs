namespace labyItems.Services;

public interface IAdvancementValidationService
{
    bool CanSave(bool hasMiracleAlignmentIssues, bool showEvilStairway, bool hasEvilStairwayValidationError);
}
