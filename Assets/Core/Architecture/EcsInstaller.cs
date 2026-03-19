using Bridge;

namespace Core.Architecture
{
    public class EcsInstaller:IInstaller
    {
        public void Register(DI.DIContainer container)
        {
            var bridge=new EcsInputBridge();
        }
    }
}