using Core.DI;
using MVVM.ViewModel.Base;

namespace MVVM.ViewModel.Interfaces
{
    public interface IViewModelFactory
    {
        T CreateScoped<T>() where T : ViewModelBase;
        T CreateTransient<T>() where T : ViewModelBase;
        T GetSingleton<T>() where T : ViewModelBase;
        T CreateForScope<T>(IScope scope) where T : ViewModelBase;
    }
}