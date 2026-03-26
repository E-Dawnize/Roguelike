using System;
using System.Collections.Generic;
using Core.Architecture;
using MVC.Controller.Interfaces;
namespace MVC.Controller.Manager
{
    public class ControllerManager:IControllerManager,IStartable,IDisposable
    {
        public List<IController> _controllers { get; }
        public void Add(IController controller)
        {
            _controllers.Add(controller);
            controller.Bind();
        }

        public ControllerManager(List<IController> controllers)
        {
            _controllers = new();
            foreach (var controller in controllers)
            {
                _controllers.Add(controller);
            }
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
            foreach (var c in _controllers)
            {
                c.Bind();
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