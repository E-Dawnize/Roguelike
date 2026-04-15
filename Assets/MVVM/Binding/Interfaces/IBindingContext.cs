using System;

namespace MVVM.Binding.Interfaces
{
    public interface IBindingContext:IDisposable
    {
        void AddBinding(IBinding binding);
        void RemoveBinding(IBinding binding);
        void Clear();
    }
}