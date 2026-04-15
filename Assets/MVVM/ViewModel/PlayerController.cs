using Bridge;
using Core.DI;
using Core.Events.EventDefinitions;
using Input.InputInterface;
using MVVM.Model;
using UnityEngine;

namespace MVVM.ViewModel
{
    public class PlayerController:ControllerBase<EntityModel>
    {
        [Inject] private readonly IPlayerInput _input;
        [Inject] private readonly IEcsInputBridge _ecsInputBridge;
        public override void Bind()
        {
            Model.Changed += OnPlayerHpChanged;
            _input.OnAttackPerformed += OnAttack;
            _input.OnMovePerformed += OnPlayerMovePerformed;
            _input.OnMoveCanceled += OnPlayerMoveCanceled;
        }

        private void OnPlayerMovePerformed(Vector2 direction)
        {
            _ecsInputBridge.SetMove(direction,true);
        }

        private void OnPlayerMoveCanceled(Vector2 direction)
        {
            _ecsInputBridge.SetMove(direction,false);
        }

        private void OnAttack()
        {
            EventCenter.Publish(new AttackEvent(Model.EntityId));
        }
        private void OnPlayerHpChanged(ModelChanged change)
        {
            EventCenter.Publish(new EntityHpChangedEvent
            {
                EntityId = Model.EntityId,
                DeltaHp = change.Delta,
                CurrentHp = change.Current
            });
        }

        public override void Unbind()
        {
            Model.Changed -= OnPlayerHpChanged;
            _input.OnAttackPerformed -= OnAttack;
            _input.OnMovePerformed -= OnPlayerMovePerformed;
            _input.OnMoveCanceled -= OnPlayerMoveCanceled;
        }

        public override void Tick(float dt)
        {
            
        }
    }
}