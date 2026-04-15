namespace Core.Architecture.Interfaces
{
    /// <summary>
    /// 纯c#类帧更新接口
    /// </summary>
    public interface ITickable
    {
        void Tick(float deltaTime);
    }
}