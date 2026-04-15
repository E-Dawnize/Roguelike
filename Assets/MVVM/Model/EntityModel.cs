using Unity.Entities;

namespace MVVM.Model
{
    public class EntityModel:ModelBase
    {
        #region BaseData

        public Entity EntityId { get; private set; }
        
        private int _currentHealth;
        public int CurrentHealth
        {
            get { return _currentHealth; }
            set
            {
                int delta = value - _currentHealth;
                _currentHealth = value;
                NotifyChanged(new ModelChanged(ModelChangeType.Hp, delta,_currentHealth));
            }
        }
        
        private int _maxHealth;

        public int MaxHealth
        {
            get { return _maxHealth; }
            set
            {
                _maxHealth=value;
            }
        }

        public EntityModel(Entity entity,int maxHealth)
        {
            EntityId = entity;
            MaxHealth = maxHealth;
        }
        public void TakeDamage(int damage){
            CurrentHealth -= damage;
        }
        #endregion
    }
}