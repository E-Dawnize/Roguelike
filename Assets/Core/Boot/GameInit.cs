using System;
using UnityEngine;
using Core.DI;
using Core.Events;
using Core.Events.EventInterfaces;
using Core.Tools;
using Factory;
using Input.InputInterface;
using Input.Manager;

namespace Core.Boot
{
    public class GameInit:MonoSingleton<GameInit>
    {
        [SerializeField] private EventManager eventManager;
        [SerializeField] private PlayerInputManager playerInputManager;
        private DIContainer _globalDIContainer;
        void DIInit()
        {
            _globalDIContainer=DIContainer.Instance;
            _globalDIContainer.RegisterSingleton(eventManager);
            _globalDIContainer.RegisterSingleton(new InputFactory());
            _globalDIContainer.RegisterSingleton<IPlayerInput>(sp => 
                sp.GetService<InputFactory>().Create()
            );
            
            _globalDIContainer.RegisterSingleton<IEventCenter>(new EventManager());
        }

        private void Awake()
        {
            DIInit();
        }
    }
}