using Core.Architecture;
using Core.DI;
using UnityEngine;

namespace Core.Boot
{
    public class ProjectContext:MonoBehaviour
    {
        private static ProjectContext _instance;
        private DIContainer _globalContainer;
        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("ProjectContext");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ProjectContext>();
            _instance.Boot();
        }

        private void Boot()
        {
            _globalContainer = new DIContainer();
            var config = LoadInstallerConfig();
            InstallGlobal(config, _globalContainer);
            Initialize(_globalContainer);
            StartAll(_globalContainer);
            SceneScopeRunner.Attach(config,_globalContainer);
        }

        InstallerConfig LoadInstallerConfig()
        {
            return Resources.Load<InstallerConfig>("Configs/BootConfig");
        }
        private void InstallGlobal(InstallerConfig installerConfig,DI.DIContainer container)
        {
            foreach (var installer in installerConfig.GlobalInstallersSorted)
            {
                installer.Register(container);
            }
        }
        private void Initialize(DI.DIContainer container)
        {
            foreach (var init in container.ResolveAll<IInitializable>())
                init.Initialize();
        }

        private void StartAll(DI.DIContainer container)
        {
            foreach (var start in container.ResolveAll<IStartable>())
                start.OnStart();
        }
    }
}