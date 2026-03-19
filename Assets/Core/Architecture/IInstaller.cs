namespace Core.Architecture
{
    public interface IInstaller
    {
        void Register(DI.DIContainer container);
    }
}