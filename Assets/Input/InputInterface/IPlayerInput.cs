using System;
using UnityEngine;

namespace Input.InputInterface
{
    public interface IPlayerInput
    {
        event Action OnAttackPerformed;
        event Action OnAttackCanceled;
        event Action<Vector2>  OnMovePerformed;
        event Action<Vector2> OnMoveCanceled;
        event Action OnJumpPerformed; 
        
        void OnEnable();
        void OnDisable();
    }
}