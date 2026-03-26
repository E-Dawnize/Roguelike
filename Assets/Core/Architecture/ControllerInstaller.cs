using Bridge;
using Core.DI;
using MVC.Controller;
using MVC.Controller.Interfaces;
using MVC.Controller.Manager;
using UnityEngine;

namespace Core.Architecture
{
    [CreateAssetMenu(fileName = "ControllerInstaller", menuName = "Boot/ControllerInstaller")] 
    public class ControllerInstaller:InstallerAsset
    {
        public override void Register(DIContainer container)
        {
            container.RegisterScoped<IControllerManager, ControllerManager>();

            
            container.RegisterScoped<IStartable, ControllerManager>();
            container.RegisterScoped<IController, PlayerController>();
            container.RegisterScoped<IEcsInputBridge, EcsInputBridge>();
        }
    }
}