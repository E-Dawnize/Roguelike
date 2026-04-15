using System;
using System.ComponentModel;
using Core.Architecture.Interfaces;

namespace MVVM.Interfaces
{
    public interface IViewModel:IInitializable,IStartable,IDisposable
    {
        event PropertyChangedEventHandler PropertyChanged;
    }
}