using System.Windows.Input;

namespace Microsoft.Maui.Controls;

public sealed class Command : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public Command(Action execute)
        : this(execute, null)
    {
    }

    public Command(Action execute, Func<bool>? canExecute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
        => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
        => _execute();

    public void ChangeCanExecute()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class Command<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public Command(Action<T?> execute)
        : this(execute, null)
    {
    }

    public Command(Action<T?> execute, Func<T?, bool>? canExecute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
        => _canExecute?.Invoke(ConvertParameter(parameter)) ?? true;

    public void Execute(object? parameter)
        => _execute(ConvertParameter(parameter));

    public void ChangeCanExecute()
        => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? ConvertParameter(object? parameter)
    {
        if (parameter is null)
            return default;

        if (parameter is T typed)
            return typed;

        return default;
    }
}
