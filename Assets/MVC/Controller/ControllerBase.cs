using MVC.Controller.Interfaces;

namespace MVC.Controller
{
    using Core.Events.EventInterfaces;

    public abstract class ControllerBase:IController
    {
        protected readonly IEventCenter EventCenter;
        protected ControllerBase(IEventCenter eventCenter) => EventCenter = eventCenter;
        public virtual void Bind() {}
        public virtual void Unbind() {}
        public void Tick(float dt) {}
    }

    public abstract class ControllerBase<TModel> : ControllerBase
    {
        protected readonly TModel Model;
        protected ControllerBase(TModel model, IEventCenter eventCenter)
            : base(eventCenter) => Model = model;
    }
}