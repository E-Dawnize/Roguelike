namespace MVVM.Binding.Interfaces
{
    public interface IBinding
    {
        void Bind();
        void UnBind();
        /// <summary>
        /// View->ViewModel
        /// </summary>
        void UpdateSource();
        
        /// <summary>
        /// ViewModel->View
        /// </summary>
        void UpdateTarget();
    }
}