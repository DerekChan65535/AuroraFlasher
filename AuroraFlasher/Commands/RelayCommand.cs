using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AuroraFlasher.Logging;

namespace AuroraFlasher.Commands
{
    /// <summary>
    /// A command whose sole purpose is to relay its functionality to other objects by invoking delegates.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;
        private readonly string _commandName;
        private static int _instanceCounter = 0;
        private readonly int _instanceId;
        private event EventHandler _canExecuteChanged;

        public event EventHandler CanExecuteChanged
        {
            add 
            { 
                _canExecuteChanged += value;
                CommandManager.RequerySuggested += value; 
            }
            remove 
            { 
                _canExecuteChanged -= value;
                CommandManager.RequerySuggested -= value; 
            }
        }

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null, string commandName = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
            _commandName = commandName ?? "UnnamedCommand";
            _instanceId = ++_instanceCounter;
            Logger.Debug($"[RelayCommand] Created: {_commandName} (ID: {_instanceId})");
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null, string commandName = null)
            : this(
                execute != null ? new Action<object>(_ => execute()) : null,
                canExecute != null ? new Predicate<object>(_ => canExecute()) : null,
                commandName)
        {
        }

        public bool CanExecute(object parameter)
        {
            bool result = _canExecute == null || _canExecute(parameter);
            Logger.Debug($"[RelayCommand] CanExecute called for '{_commandName}' (ID: {_instanceId}): {result}");
            return result;
        }

        public void Execute(object parameter)
        {
            Logger.Debug($"[RelayCommand] Execute called for '{_commandName}' (ID: {_instanceId})");
            _execute(parameter);
            Logger.Debug($"[RelayCommand] Execute completed for '{_commandName}' (ID: {_instanceId})");
        }

        public void RaiseCanExecuteChanged()
        {
            Logger.Debug($"[RelayCommand] RaiseCanExecuteChanged called for '{_commandName}' (ID: {_instanceId})");
            // Directly raise the event for this specific command (best practice)
            _canExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// An async command that supports async/await patterns
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _executeAsync;
        private readonly Predicate<object> _canExecute;
        private bool _isExecuting;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    RaiseCanExecuteChanged();
                }
            }
        }

        public AsyncRelayCommand(Func<object, Task> executeAsync, Predicate<object> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
            : this(
                executeAsync != null ? new Func<object, Task>(_ => executeAsync()) : null,
                canExecute != null ? new Predicate<object>(_ => canExecute()) : null)
        {
        }

        public bool CanExecute(object parameter)
        {
            return !IsExecuting && (_canExecute == null || _canExecute(parameter));
        }

        public async void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                try
                {
                    IsExecuting = true;
                    await _executeAsync(parameter);
                }
                finally
                {
                    IsExecuting = false;
                }
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
