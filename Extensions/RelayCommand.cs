using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Extensions;

public class RelayAsyncCommand<T> : RelayCommand<T>
{
    public RelayAsyncCommand(Action<T> execute) : base(execute)
    {
    }
    public RelayAsyncCommand(Action<T> execute, Predicate<T> canExecute) : base(execute, canExecute)
    {
    }

    public event EventHandler Ended;

    public event EventHandler Started;

    public bool IsExecuting { get; private set; }

    public override bool CanExecute(object parameter) { return base.CanExecute(parameter) && !IsExecuting; }

    public override void Execute(object parameter)
    {
        try
        {
            IsExecuting = true;
            Started?.Invoke(this, EventArgs.Empty);

            Task task = Task.Run(() => _execute((T)parameter));
            _ = task.ContinueWith(_ => OnRunWorkerCompleted(EventArgs.Empty), TaskScheduler.FromCurrentSynchronizationContext());
        } catch(Exception ex)
        {
            OnRunWorkerCompleted(new RunWorkerCompletedEventArgs(null, ex, true));
        }
    }

    private void OnRunWorkerCompleted(EventArgs e)
    {
        IsExecuting = false;
        Ended?.Invoke(this, e);
    }
}

public class RelayCommand<T> : ICommand
{
    #region Fields
    protected readonly Predicate<T> _canExecute;

    protected readonly Action<T> _execute;
    #endregion Fields

    #region Constructors
    public RelayCommand(Action<T> execute) : this(execute, null)
    {
    }

    public RelayCommand(Action<T> execute, Predicate<T> canExecute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }
    #endregion Constructors

    #region ICommand Members
    public event EventHandler CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }

    [DebuggerStepThrough]
    public virtual bool CanExecute(object parameter) { return _canExecute == null || _canExecute((T)parameter); }

    [DebuggerStepThrough]
    public virtual void Execute(object parameter) { _execute((T)parameter); }
    #endregion ICommand Members
}

public class RelayCommand : ICommand
{
    protected readonly Func<bool> canExecute;
    protected readonly Action execute;

    public RelayCommand(Action execute) : this(execute, null)
    {
    }

    public RelayCommand(Action execute, Func<bool> canExecute)
    {
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
        add
        {
            if(canExecute != null)
            {
                CommandManager.RequerySuggested += value;
            }
        }

        remove
        {
            if(canExecute != null)
            {
                CommandManager.RequerySuggested -= value;
            }
        }
    }

    public virtual bool CanExecute(object parameter) { return canExecute == null || canExecute(); }
    public virtual void Execute(object parameter) { execute(); }
}