using System.Collections.Generic;
using MVC.Controller.Interfaces;
namespace MVC.Controller.Manager
{
    public class ControllerManager
    {
        private readonly List<IController> _controllers = new();
        public void Add(IController controller)
        {
            _controllers.Add(controller);
            controller.Bind();
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

        public void Tick(float dt)
        {
            foreach (var c in _controllers)
                c.Tick(dt);
        }
    }
}