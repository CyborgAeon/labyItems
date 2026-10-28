namespace labyItems.Models.Authentication;

public sealed record AccountProfile(string FirstName, string LastName)
{
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}
