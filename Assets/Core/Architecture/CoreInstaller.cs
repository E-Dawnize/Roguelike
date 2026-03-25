using Core.Events;
using Core.Events.EventInterfaces;
using Input.InputInterface;
using Input.Manager;
using MVC.Controller.Interfaces;
using MVC.Controller.Manager;
using UnityEngine;

namespace Core.Architecture
{
    [CreateAssetMenu(fileName = "CoreInstaller", menuName = "Boot/CoreInstaller")]
    public class CoreInstaller:InstallerAsset
    {
        public override void Register(DI.DIContainer container)
        {
            container.RegisterSingleton<IEventCenter>(new EventManager());
            container.RegisterSingleton<IPlayerInput>(sp =>
            {
                var go = new GameObject("PlayerInputManager");
                Object.DontDestroyOnLoad(go);
                var mgr = go.AddComponent<PlayerInputManager>();
                mgr.Initialize();
                return mgr;
            });
        }
    }
}