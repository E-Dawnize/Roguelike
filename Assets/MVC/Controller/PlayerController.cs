using Bridge;
using Core.Events.EventDefinitions;
using Input.InputInterface;
using MVC.Controller.Interfaces;
using MVC.Model;
using UnityEngine;

namespace MVC.Controller
{
    using Core.Events.EventInterfaces;
    public class PlayerController:ControllerBase<EntityModel>,IController
    {
        private readonly IPlayerInput _input;
        private readonly IEcsInputBridge _ecsInputBridge;
        public virtual void Bind()
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

        public virtual void Unbind()
        {
            Model.Changed -= OnPlayerHpChanged;
            _input.OnAttackPerformed -= OnAttack;
        }

        PlayerController(EntityModel model, IEventCenter eventCenter, IPlayerInput input,IEcsInputBridge bridge) : base(model, eventCenter)
        {
            _input = input;
            _ecsInputBridge = bridge;
        }
    }
}