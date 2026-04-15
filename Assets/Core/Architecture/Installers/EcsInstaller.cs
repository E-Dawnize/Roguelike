using Bridge;
using UnityEngine;

namespace Core.Architecture.Installers
{
    [CreateAssetMenu(fileName = "EcsInstaller", menuName = "Boot/EcsInstaller")]
    public class EcsInstaller:InstallerAsset
    {
        public override void Register(DI.DIContainer container)
        {
            
        }
    }
}