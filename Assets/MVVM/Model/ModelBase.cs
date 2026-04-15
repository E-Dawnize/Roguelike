using System;

namespace MVVM.Model
{
    public enum ModelChangeType { Hp, MaxHp, Attack, Speed }

    public readonly struct ModelChanged
    {
        public readonly ModelChangeType Type;
        public readonly int Delta;
        public readonly int Current;
        public ModelChanged(ModelChangeType type, int delta, int current)
        {
            Type = type; Delta = delta; Current = current;
        }
    }
    public abstract class ModelBase
    {
        public event Action<ModelChanged> Changed;
        protected void NotifyChanged(ModelChanged evt) => Changed?.Invoke(evt);
    }
}