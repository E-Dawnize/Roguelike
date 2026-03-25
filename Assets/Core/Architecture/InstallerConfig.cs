using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Core.Architecture
{
    [CreateAssetMenu(fileName = "BootConfig", menuName = "Boot/InstallerConfig")]
    public class InstallerConfig:ScriptableObject
    {
        public List<InstallerAsset> globalInstallers = new();
        public List<InstallerAsset> sceneInstallers = new();
        public IEnumerable<InstallerAsset> GlobalInstallersSorted =>
            globalInstallers.OrderBy(i => i.order);

        public IEnumerable<InstallerAsset> SceneInstallersSorted =>
            sceneInstallers.OrderBy(i => i.order);
    }
}