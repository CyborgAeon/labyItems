namespace labyItems.Controls;

/// <summary>Notifies the login page when a credential provider fills an entry.</summary>
public sealed class LoginEntry : Entry
{
    public event Action? Autofilled;
    public void NotifyAutofilled() => Autofilled?.Invoke();
}
