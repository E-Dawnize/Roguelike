using Core.Architecture.Interfaces;

namespace Core.Architecture
{
    using UnityEngine;
    public class InstallerAsset : ScriptableObject, IInstaller
    {
        public int order=0;

        public virtual void Register(DI.DIContainer container)
        {
            Debug.Log($"Not Override");
        }
    }
}
