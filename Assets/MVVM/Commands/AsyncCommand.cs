using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MVVM.Commands
{
    public class AsyncCommand: ICommand
    {
        public event EventHandler CanExecuteChanged;
        
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting;

        public AsyncCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }
        public bool CanExecute(object  parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }
        public async void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                try
                {
                    _isExecuting = true;
                    RaiseCanExecuteChanged();

                    await _executeAsync();
                }
                finally
                {
                    _isExecuting = false;
                    RaiseCanExecuteChanged();
                }
            }
        }
        private void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public class AsyncCommand<T> : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly Func<T, Task> _executeAsync;
        private readonly Func<T, bool> _canExecute;
        private bool _isExecuting;

        public AsyncCommand(Func<T, Task> executeAsync, Func<T, bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (!_isExecuting && parameter is T typedParameter)
            {
                return _canExecute?.Invoke(typedParameter) ?? true;
            }
            return false;
        }

        public async void Execute(object parameter)
        {
            if (CanExecute(parameter) && parameter is T typedParameter)
            {
                try
                {
                    _isExecuting = true;
                    RaiseCanExecuteChanged();

                    await _executeAsync(typedParameter);
                }
                finally
                {
                    _isExecuting = false;
                    RaiseCanExecuteChanged();
                }
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}