using Core.DI;
using Core.Events.EventInterfaces;
using MVVM.ViewModel.Interfaces;
using UnityEngine;

namespace MVVM.ViewModel
{
    public abstract class ControllerBase:IController
    {
        [Inject] protected IEventCenter EventCenter;
        public virtual void Bind() {}
        public virtual void Unbind() {}

        public virtual void Tick(float dt)
        {
            Debug.LogWarning("Default tick");
        }
    }

    public abstract class ControllerBase<TModel> : ControllerBase
    {
        [Inject]protected readonly TModel Model;
    }
}