using Input.InputInterface;
using Input.Manager;

namespace Core.Architecture
{
    public class InputInstaller:IInstaller
    {
        private readonly PlayerInputManager _playerInputManager;
        public InputInstaller(PlayerInputManager playerInputManager)
        {
            _playerInputManager = playerInputManager;
        }
        public void Register(DI.DIContainer container)
        {
            container.RegisterSingleton<IPlayerInput>(_playerInputManager);
        }
    }
}