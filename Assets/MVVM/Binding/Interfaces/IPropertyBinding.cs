namespace MVVM.Binding.Interfaces
{
    public interface IPropertyBinding:IBinding
    {
        //数据源
        string SourceProperty { get; }
        //UI目标
        string TargetProperty { get; }
        BindingMode BindingMode { get; }
        IValueConverter Converter { get; }
    }
}