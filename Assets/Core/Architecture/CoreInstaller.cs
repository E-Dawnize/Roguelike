using Core.Events;
using Core.Events.EventInterfaces;
using MVC.Controller.Manager;

namespace Core.Architecture
{
    public class CoreInstaller:IInstaller
    {
        public void Register(DI.DIContainer container)
        {
            container.RegisterSingleton<IEventCenter>(new EventManager());
            container.RegisterSingleton(new ControllerManager());
        }
    }
}