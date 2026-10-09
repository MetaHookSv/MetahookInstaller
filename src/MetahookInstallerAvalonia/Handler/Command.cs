using System;
using System.Windows.Input;

namespace MetahookInstallerAvalonia.Handler;

public class Command(Action<object?> execute, Func<object?, bool> canExecute) : ICommand
{
    private readonly Action<object?> _execute = execute;
    private readonly Func<object?, bool> _canExecute = canExecute;

    // CanExecute never changes at runtime, so the event has no subscribers to notify.
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter)
    {
        if (_canExecute != null)
        {
            return _canExecute(parameter);
        }
        else
        {
            return false;
        }
    }

    public void Execute(object? parameter)
    {
        _execute(parameter);
    }
}
