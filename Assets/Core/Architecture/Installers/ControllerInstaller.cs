using Bridge;
using Core.Architecture.Interfaces;
using Core.DI;
using MVVM.ViewModel;
using MVVM.ViewModel.Interfaces;
using MVVM.ViewModel.Manager;
using UnityEngine;

namespace Core.Architecture.Installers 
{
    [CreateAssetMenu(fileName = "ControllerInstaller", menuName = "Boot/ControllerInstaller")] 
    public class ControllerInstaller:InstallerAsset
    {
        public override void Register(DIContainer container)
        {
            container.RegisterScoped<IControllerManager, ControllerManager>();

            
            container.RegisterScoped<IStartable, ControllerManager>();
            container.RegisterScoped<IController, PlayerController>();
        }
    }
}