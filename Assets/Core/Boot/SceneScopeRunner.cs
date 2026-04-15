using Core.Architecture;
using Core.Architecture.Interfaces;
using Core.DI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Boot
{
    public class SceneScopeRunner:MonoBehaviour
    {
        private InstallerConfig _config;
        private IScope _scope;
        private DIContainer _globalContainer;
        public static void Attach(InstallerConfig config,DIContainer globalContainer)
        {
            var go = new GameObject("SceneScopeRunner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<SceneScopeRunner>();
            runner._config = config;
            runner._globalContainer = globalContainer;
            SceneManager.sceneLoaded += runner.OnSceneLoaded;
            SceneManager.sceneUnloaded += runner.OnSceneUnloaded;
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _scope = _globalContainer.CreateScope();
            foreach (var installer in _config.SceneInstallersSorted)
                installer.Register(_globalContainer);
            Initialize(_scope);
            StartAll(_scope);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            _scope?.Dispose();
            _scope = null;
        }
        private void Initialize(IScope scope)
        {
            foreach (var init in _globalContainer.ResolveAll<IInitializable>(scope))
                init.Initialize();
        }

        private void StartAll(IScope scope)
        {
            foreach (var start in _globalContainer.ResolveAll<IStartable>(scope))
                start.OnStart();
        }
    }
}