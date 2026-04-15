using System;
using System.Collections.Generic;
using Core.Architecture.Interfaces;
using Core.DI;
using MVVM.ViewModel.Interfaces;

namespace MVVM.ViewModel.Manager
{
    public class ControllerManager:IControllerManager,IStartable,IDisposable, ITickable
    {
        [Inject]public List<IController> _controllers { get; }
        public void Add(IController controller)
        {
            _controllers.Add(controller);
            controller.Bind();
        }

        public ControllerManager()
        {
            _controllers = new();
        }
        
        public void Remove(IController controller)
        {
            controller.Unbind();
            _controllers.Remove(controller);
        }
        public void Shutdown()
        {
            foreach (var c in _controllers)
                c.Unbind();
            _controllers.Clear();
        }

        public void OnStart()
        {
            if (_controllers != null)
            {
                foreach (var controller in _controllers)
                {
                    Add(controller);
                }
            }
        }

        public void Tick(float dt)
        {
            foreach (var c in _controllers)
                c.Tick(dt);
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}