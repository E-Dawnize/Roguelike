using System.Collections.Generic;

namespace MVVM.Binding.Interfaces
{
    public interface IBindingManager
    {

        // 绑定管理
        void RegisterBinding(IBinding binding, object context = null);
        void UnregisterBinding(IBinding binding, object context = null);

        // 上下文管理
        void BindAllInContext(object context);
        void UnbindAllInContext(object context);
        void RebindAllInContext(object context);

        // 批量操作
        void BindAll();
        void UnbindAll();
        void RebindAll();

        // 查询与调试
        int GetBindingCount();
        List<IBinding> GetBindingsInContext(object context);
        string GetDebugInfo();
    }
}