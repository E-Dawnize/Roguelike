using Input.InputInterface;
using Input.Manager;
using UnityEngine;

namespace Factory
{
    public sealed class InputFactory
    {
        public IPlayerInput Create()
        {
            var go=new GameObject("PlayerInputManager");
            Object.DontDestroyOnLoad(go);
            var mgr=go.AddComponent<PlayerInputManager>();
            mgr.Initialize();
            return mgr;
        }
    }
}