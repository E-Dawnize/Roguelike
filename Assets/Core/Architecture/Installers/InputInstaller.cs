using Core.Architecture.Interfaces;
using Core.DI;
using Input.InputInterface;
using Input.Manager;
using UnityEngine;

namespace Core.Architecture.Installers
{
    [CreateAssetMenu(fileName = "InputInstaller", menuName = "Boot/InputInstaller")]
    public class InputInstaller:InstallerAsset
    {
        public override void Register(DI.DIContainer container)
        {
            container.RegisterSingleton<IPlayerInput>(sp =>
            {
                var go=new GameObject("PlayerInputManager");
                var mgr=go.AddComponent<PlayerInputManager>();
                DontDestroyOnLoad(go);
                return mgr;
            });
            container.RegisterSingleton<IInitializable>(sp =>
                (IInitializable)((DIContainer)sp).GetRequiredService<IPlayerInput>());
        }
    }
}