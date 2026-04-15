namespace MVVM.Binding.Interfaces
{
    public interface ICommandBinding:IBinding
    {
        string CommandName { get; }
        string EventName { get; }
    }
}