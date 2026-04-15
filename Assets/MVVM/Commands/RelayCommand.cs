using System;
using System.Windows.Input;
using UnityEngine;

namespace MVVM.Commands
{
    public class RelayCommand:ICommand
    {
        public event EventHandler CanExecuteChanged;
        private readonly Action _execute;
        private readonly Func<object, bool> _canExecute;

        public void Execute(object parameter=null)
        {
            try
            {
                _execute();
            }
            catch (Exception ex)
            {
                // 错误处理：记录日志或抛出封装异常
                Debug.LogError($"命令执行失败: {ex.Message}");
                throw;
            }
        }

        public bool CanExecute(object parameter)
        {

            return _canExecute?.Invoke(parameter) ?? true;
        }
        public RelayCommand(Action execute, Func<object, bool> canExecute=null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
    }
    public class RelayCommand<T>:ICommand
    {
        public event EventHandler CanExecuteChanged;
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public void Execute(object parameter)
        {
            try
            {
                _execute(parameter is T  typedParameter ? typedParameter : default(T));
            }
            catch (Exception ex)
            {
                // 错误处理：记录日志或抛出封装异常
                Debug.LogError($"命令执行失败: {ex.Message}");
                throw;
            }
        }

        public bool CanExecute(object parameter)
        {

            return _canExecute?.Invoke(parameter is T  typedParameter ? typedParameter : default(T)) ?? true;
        }
        public RelayCommand(Action<T> execute, Func<T, bool> canExecute=null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
    }
}