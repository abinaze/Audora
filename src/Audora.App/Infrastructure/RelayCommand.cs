using System.Windows.Input;

namespace Audora.App.Infrastructure;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _run;
    private readonly Func<object?, bool>? _canRun;

    public RelayCommand(Action run, Func<bool>? canRun = null)
    {
        _run = _ => run();
        if (canRun is not null)
        {
            _canRun = _ => canRun();
        }
    }

    public RelayCommand(Action<object?> run, Func<object?, bool>? canRun = null)
    {
        _run = run;
        _canRun = canRun;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canRun?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _run(parameter);
}
