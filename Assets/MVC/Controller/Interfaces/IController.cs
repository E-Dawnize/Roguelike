namespace MVC.Controller.Interfaces
{
    public interface IController
    {
        void Bind();
        void Unbind();
        void Tick(float dt);
    }
}