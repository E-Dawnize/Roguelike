using System.Collections.Generic;

namespace MVC.Controller.Interfaces
{
    public interface IControllerManager
    {
        List<IController> _controllers { get; }
        public void Add(IController controller);

        public void Remove(IController controller);

        public void Shutdown();

        public void Tick(float dt);
    }
}