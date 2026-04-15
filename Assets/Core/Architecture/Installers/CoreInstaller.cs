using Bridge;
using Core.Events;
using Core.Events.EventInterfaces;
using Input.InputInterface;
using Input.Manager;
using MVVM.ViewModel.Factory;
using MVVM.ViewModel.Interfaces;
using Unity.Entities;
using UnityEngine;

namespace Core.Architecture.Installers
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
            container.RegisterSingleton<IEcsInputBridge>(sp =>
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                {
                    Debug.LogError("ECS World not found!");
                    return null;
                }
                return new EcsInputBridge(world.EntityManager);
            });
            container.RegisterSingleton<IViewModelFactory, ViewModelFactory>();
        }
    }
}