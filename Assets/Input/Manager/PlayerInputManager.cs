using Core.Tools;
using System;
using Core.Architecture;
using Core.Architecture.Interfaces;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Input.InputInterface;
using Input.InputConfig;
namespace Input.Manager
{
    /// <summary>
    /// 玩家输入系统实现，唯一输入入口
    /// </summary>
    public class PlayerInputManager:MonoBehaviour,IPlayerInput,IInitializable
    {
        public event Action OnAttackPerformed;
        public event Action OnAttackCanceled;
        public event Action OnJumpPerformed; 
        public event Action<Vector2>  OnMovePerformed;
        public event Action<Vector2> OnMoveCanceled;
        
        public bool IsReady { get;private set; }
        private PlayerInputActions _playerInputActions;//inputAction C#类

        public void Initialize()
        {
            if(IsReady)return;
            _playerInputActions = new PlayerInputActions();
            BindInputCallbacks();
            IsReady = true;
            if(isActiveAndEnabled)
                _playerInputActions.Gameplay.Enable();
        }

        private void BindInputCallbacks()
        {
            _playerInputActions.Gameplay.Attack.performed += ctx => OnAttackPerformed?.Invoke();
            _playerInputActions.Gameplay.Jump.performed+=ctx=>OnJumpPerformed?.Invoke();
            _playerInputActions.Gameplay.Attack.canceled+=ctx=>OnAttackCanceled?.Invoke();
            _playerInputActions.Gameplay.Move.performed += ctx => 
                OnMovePerformed?.Invoke(ctx.ReadValue<Vector2>());
            _playerInputActions.Gameplay.Move.canceled += ctx => 
                OnMoveCanceled?.Invoke(Vector2.zero);
        }

        public void OnEnable()
        {
            if(IsReady)
                _playerInputActions?.Gameplay.Enable();
        }

        public void OnDisable()
        {
            if(IsReady)
                _playerInputActions?.Gameplay.Disable();
        }
        private void OnDestroy()
        {
            if (_playerInputActions != null)
                Addressables.Release(_playerInputActions);
        }
    }
}