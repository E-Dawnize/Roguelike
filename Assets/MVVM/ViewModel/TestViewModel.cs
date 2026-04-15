using System.ComponentModel;
using MVVM.ViewModel.Base;

namespace MVVM.ViewModel
{
    public class TestViewModel:ViewModelBase
    {
        private string testString;
        public string TestString
        {
            set=> SetProperty(ref testString, value);
            get { return testString; }
        }
    }
}