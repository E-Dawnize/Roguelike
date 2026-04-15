using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Core.DI;
using Core.Events.EventInterfaces;
using MVVM.Commands;
using MVVM.Interfaces;

namespace MVVM.ViewModel.Base
{
    public abstract class ViewModelBase:INotifyPropertyChanged,IViewModel
    {
        public event PropertyChangedEventHandler PropertyChanged;
        [Inject] protected IEventCenter EventCenter;
        private readonly Dictionary<string, ICommand> _commands = new();
        private bool _isInitialized;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected void RegisterCommand(string name, ICommand command)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Command name cannot be null or empty", nameof(name));

            _commands[name] = command;
        }
        protected void UnregisterCommand(string name)
        {
            _commands.Remove(name);
        }

        protected ICommand GetCommand(string name)
        {
            return _commands.TryGetValue(name, out var command) ? command : null;
        }
        protected ICommand CreateCommand(Action execute, Func<bool> canExecute = null)
        {
            return new RelayCommand(
                execute,
                _ => canExecute?.Invoke() ?? true
            );
        }
        protected ICommand CreateCommand<T>(Action<T> execute, Func<T, bool> canExecute = null)
        {
            return new RelayCommand<T>(
                execute ?? throw new ArgumentNullException(nameof(execute)),
                canExecute
            );
        }

        protected ICommand CreateAsyncCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            return new AsyncCommand(executeAsync, canExecute);
        }

        protected ICommand CreateAsyncCommand<T>(Func<T, Task> executeAsync, Func<T, bool> canExecute =
            null)
        {
            return new AsyncCommand<T>(executeAsync, canExecute);
        }
        public virtual bool SetProperty<T>(ref T property, T value, [CallerMemberName] string propertyName = "")
        {
            if(value.Equals(property))return false;
            property = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        
        public virtual void Initialize() { }
        public void OnStart() { }
        public virtual void Dispose() { }
    }
}