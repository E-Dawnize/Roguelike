using System;
using System.Reflection;
using System.Windows.Input;
using Core.DI;
using MVVM.Binding.Interfaces;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MVVM.Binding
{
    public enum BindingParameterType
    {
        None,
        FixedValue,
        PropertyPath,
        EventArgument
    }
    /// <summary>
    /// UI事件到数据实体的绑定
    /// </summary>
    public class CommandBinding:MonoBehaviour,ICommandBinding
    {
        [Header("绑定配置")]
        [SerializeField] private MonoBehaviour _viewModel;
        [SerializeField] private string _commandName;
        [SerializeField] private string _eventName = "onClick";
        [SerializeField] private Component _targetComponent;

        [Header("命令参数")]
        [SerializeField] private BindingParameterType _parameterType = BindingParameterType.None;
        [SerializeField] private string _parameterPropertyPath;
        [SerializeField] private string _parameterValue;
        
        [Inject]private IValueConverter _valueConverter;
        private ICommand _command;              // ICommand实例缓存
        private MethodInfo _methodInfo;         // 普通方法缓存
        private PropertyInfo _propertyInfo;     // 属性缓存
        private UnityEvent _unityEvent;         // UI事件引用
        private FieldInfo _eventFieldInfo;      // 事件字段信息
        private bool _isBound = false;          // 绑定状态标志
        private object _lastEventArgument;
        private Type _commandParameterType;     // 命令参数类型缓存
        private PropertyInfo[] _propertyPathCache; // PropertyPath属性链缓存
        
        public string CommandName => _commandName;
        public string EventName => _eventName;
        private void Start()
        {
            if (!ValidateBindings())
            {
                Debug.LogError("CommandBinding: Cannot bind due to validation failure", this);
                return;
            }
            Bind();
        }

        private void OnDestroy()
        {
            UnBind();
        }

        private bool ValidateBindings()
        {
            #region 命令查找
            if (_viewModel == null)
            {
                Debug.LogError("CommandBinding: Cannot bind due to ViewModel null", this);
                return false;
            }

            if (_targetComponent == null)
            {
                Debug.LogError("CommandBinding: Cannot bind due to target component null", this);
                return false;
            }
            if (string.IsNullOrEmpty(_commandName))
            {
                Debug.LogError("CommandBinding: Command name is empty", this);
                return false;
            }
            _propertyInfo = _viewModel.GetType().GetProperty(_commandName);
            if (_propertyInfo != null)
            {
                // 检查属性类型是否为ICommand
                if (typeof(ICommand).IsAssignableFrom(_propertyInfo.PropertyType))
                {
                    _command = _propertyInfo.GetValue(_viewModel) as ICommand;
                    if (_command == null)
                    {
                        Debug.LogError($"CommandBinding: Property '{_commandName}' exists but returns null ICommand", this);
                        return false;
                    }
                }
                else
                {
                    // 属性但不是ICommand，当作方法查找
                    _propertyInfo = null;
                }
            }
            // 如果不是ICommand属性，查找方法
            if (_propertyInfo == null)
            {
                _methodInfo = _viewModel.GetType().GetMethod(_commandName);
                if (_methodInfo == null)
                {
                    Debug.LogError($"CommandBinding: Could not find command property or method '" +
                                   $"{_commandName}' on{_viewModel.GetType().Name}", this);
                    return false;
                }
            }
            

            #endregion

            #region UI事件查找

            // 对于标准UGUI组件，直接使用组件的事件属性
            // 对于自定义组件，尝试通过反射查找事件字段
            if (!IsStandardUIComponent(_targetComponent))
            {
                _eventFieldInfo = _targetComponent.GetType().GetField(_eventName, BindingFlags.Public | BindingFlags.Instance);
                if (_eventFieldInfo == null)
                {
                    Debug.LogError($"CommandBinding: Event '{_eventName}' not found on component {_targetComponent.GetType().Name}", this);
                    return false;
                }

                _unityEvent = _eventFieldInfo.GetValue(_targetComponent) as UnityEvent;
                if (_unityEvent == null)
                {
                    Debug.LogWarning($"CommandBinding: Event '{_eventName}' is not a UnityEvent, will try standard event subscription");
                }
            }

            #endregion

            #region 参数配置验证

            if (_parameterType == BindingParameterType.PropertyPath && string.IsNullOrEmpty(_parameterPropertyPath))
            {
                Debug.LogError("CommandBinding: PropertyPath参数类型需要设置参数属性路径", this);
                return false;
            }

            if (_parameterType == BindingParameterType.FixedValue && string.IsNullOrEmpty(_parameterValue))
            {
                Debug.LogWarning("CommandBinding: FixedValue参数类型设置了空值");
            }

            // 初始化PropertyPath缓存（如果使用）
            if (_parameterType == BindingParameterType.PropertyPath && !string.IsNullOrEmpty(_parameterPropertyPath))
            {
                CachePropertyPath();
            }

            #endregion

            return true;
        }
        public void Bind()
        {
            if(_isBound)return;

            // 根据组件类型订阅事件
            SubscribeToUIEvent();

            // 监听CanExecuteChanged（如果是ICommand）
            if (_command != null)
            {
                _command.CanExecuteChanged += OnCanExecuteChanged;
                // 初始更新UI状态
                UpdateTarget();
            }

            _isBound = true;
            Debug.Log($"CommandBinding: {_commandName} ← {_eventName} bound");
        }

        public void UnBind()
        {
            if(!_isBound)return;

            // 取消事件订阅
            UnsubscribeFromUIEvent();

            // 取消CanExecuteChanged监听
            if (_command != null)
            {
                _command.CanExecuteChanged -= OnCanExecuteChanged;
            }

            // 清理缓存
            _lastEventArgument = null;
            _commandParameterType = null;
            _propertyPathCache = null;

            _isBound = false;
            Debug.Log($"CommandBinding: {_commandName} ← {_eventName} unbound");
        }

        /// <summary>
        /// View->ViewModel (UI→ViewModel，命令绑定中此方向不适用)
        /// </summary>
        public void UpdateSource()
        {
            // 命令绑定主要是UI→ViewModel，所以这个方向不适用
            // 保留空实现以满足接口要求
        }

        /// <summary>
        /// ViewModel->View (根据命令的CanExecute状态更新UI交互状态)
        /// </summary>
        public void UpdateTarget()
        {
            if (_command == null) return;

            // 获取参数（暂用null）
            object parameter = GetCommandParameter();
            bool canExecute = _command.CanExecute(parameter);

            // 更新Selectable组件的interactable状态
            if (_targetComponent is Selectable selectable)
            {
                selectable.interactable = canExecute;
            }
        }

        // 事件处理器方法 - 重载支持不同事件参数类型
        private void OnUIEvent() => HandleUIEvent(null);
        private void OnUIEvent(string arg) => HandleUIEvent(arg);
        private void OnUIEvent(bool arg) => HandleUIEvent(arg);
        private void OnUIEvent(float arg) => HandleUIEvent(arg);
        private void OnUIEvent(int arg) => HandleUIEvent(arg);

        private void ExecuteCommand()
        {
            try
            {
                object parameter = GetCommandParameter();
                if (_command != null)
                {
                    if (_command.CanExecute(parameter))
                    {
                        _command.Execute(parameter);
                    }
                }
                else if(_methodInfo != null)
                {
                    var parameterInfo = _methodInfo.GetParameters();
                    if (parameterInfo.Length == 0)
                    {
                        _methodInfo.Invoke(_viewModel,null);
                    }
                    else if(parameterInfo.Length == 1)
                    {
                        _methodInfo.Invoke(_viewModel,new []{parameter});
                    }
                    else
                    {
                        Debug.LogError($"方法{_commandName}参数数量不支持");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"CommandBinding执行失败: {e.Message}", this);
            }
        }

        private void OnCanExecuteChanged(object sender, EventArgs e)
        {
            UpdateTarget(); // ViewModel状态变化，更新UI
        }

        private object GetCommandParameter()
        {
            return _parameterType switch
            {
                BindingParameterType.None => null,
                BindingParameterType.FixedValue => ParseFixedValue(),
                BindingParameterType.PropertyPath => GetPropertyValue(),
                BindingParameterType.EventArgument => GetEventArgument(),
                _ => null
            };
        }
        private object ParseFixedValue()
        {
            try
            {
                // 1. 确定目标类型
                Type targetType = GetCommandParameterType();
                if (targetType == null)
                {
                    Debug.LogWarning("CommandBinding: 无法确定命令参数类型，将传递原始字符串");
                    return _parameterValue;
                }

                // 2. 使用ValueConverter转换（如果可用）
                if (_valueConverter != null && _valueConverter.CanConvert(typeof(string), targetType))
                    return _valueConverter.Convert(_parameterValue, targetType);

                // 3. 基础类型转换（fallback）
                if (targetType == typeof(string))
                    return _parameterValue;
                else if (targetType == typeof(bool) && bool.TryParse(_parameterValue, out bool boolResult))
                    return boolResult;
                else if (targetType == typeof(int) && int.TryParse(_parameterValue, out int intResult))
                    return intResult;
                else if (targetType == typeof(float) && float.TryParse(_parameterValue, out float floatResult))
                    return floatResult;
                else if (targetType.IsEnum)
                {
                    try
                    {
                        return Enum.Parse(targetType, _parameterValue);
                    }
                    catch
                    {
                        // 尝试解析为数值
                        if (int.TryParse(_parameterValue, out int enumValue))
                        {
                            return Enum.ToObject(targetType, enumValue);
                        }
                        Debug.LogError($"CommandBinding: 无法将 '{_parameterValue}' 解析为枚举 {targetType.Name}");
                        return null;
                    }
                }
                else
                {
                    try
                    {
                        return System.Convert.ChangeType(_parameterValue, targetType);
                    }
                    catch
                    {
                        Debug.LogError($"CommandBinding: 无法将 '{_parameterValue}' 转换为 {targetType.Name}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"CommandBinding: 固定值参数转换失败 '{_parameterValue}': {ex.Message}");
                return null;
            }
        }
        private object GetPropertyValue()
        {
            if (_viewModel == null || string.IsNullOrEmpty(_parameterPropertyPath))
                return null;

            // 使用缓存属性链（如果可用）
            if (_propertyPathCache != null && _propertyPathCache.Length > 0)
            {
                object currentObj = _viewModel;
                foreach (var propInfo in _propertyPathCache)
                {
                    if (propInfo == null || currentObj == null)
                        return null;
                    currentObj = propInfo.GetValue(currentObj);
                }
                return currentObj;
            }
            else
            {
                // 回退到动态解析
                string[] pathParts = _parameterPropertyPath.Split('.');
                object currentObj = _viewModel;

                foreach (var part in pathParts)
                {
                    var propInfo = currentObj.GetType().GetProperty(part);
                    if (propInfo == null) return null;
                    currentObj = propInfo.GetValue(currentObj);
                }
                return currentObj;
            }
        }
        private void HandleUIEvent(object eventArg)
        {
            _lastEventArgument = eventArg; // 缓存供GetEventArgument使用
            ExecuteCommand(); // 调用命令执行逻辑
        }

        private object GetEventArgument()
        {
            object rawArg = _lastEventArgument;

            // 如果命令期望特定类型，进行转换
            Type expectedType = GetCommandParameterType();
            if (expectedType != null && rawArg != null && rawArg.GetType() != expectedType)
            {
                // 使用ValueConverter转换
                if (_valueConverter != null && _valueConverter.CanConvert(rawArg.GetType(), expectedType))
                    return _valueConverter.Convert(rawArg, expectedType);

                // 尝试基础类型转换
                try
                {
                    return System.Convert.ChangeType(rawArg, expectedType);
                }
                catch
                {
                    Debug.LogWarning($"CommandBinding: 无法将事件参数类型 {rawArg.GetType().Name} 转换为 {expectedType.Name}");
                }
            }

            return rawArg;
        }

        /// <summary>
        /// 获取命令期望的参数类型
        /// </summary>
        private Type GetCommandParameterType()
        {
            // 缓存参数类型避免重复反射
            if (_commandParameterType != null)
                return _commandParameterType;

            if (_command != null)
            {
                // ICommand接口：获取Execute方法的参数类型
                var executeMethod = _command.GetType().GetMethod("Execute");
                if (executeMethod != null)
                {
                    var parameters = executeMethod.GetParameters();
                    _commandParameterType = parameters.Length > 0 ? parameters[0].ParameterType : null;
                }
            }
            else if (_methodInfo != null)
            {
                // 普通方法：获取第一个参数的类型
                var parameters = _methodInfo.GetParameters();
                _commandParameterType = parameters.Length > 0 ? parameters[0].ParameterType : null;
            }
            else
            {
                _commandParameterType = null; // 无法确定类型
            }

            return _commandParameterType;
        }

        /// <summary>
        /// 检查是否是标准UGUI组件
        /// </summary>
        private bool IsStandardUIComponent(Component component)
        {
            return component is Button ||
                   component is Toggle ||
                   component is InputField ||
                   component is Slider ||
                   component is Dropdown;
        }

        /// <summary>
        /// 缓存PropertyPath属性链
        /// </summary>
        private void CachePropertyPath()
        {
            if (string.IsNullOrEmpty(_parameterPropertyPath) || _viewModel == null)
                return;

            string[] pathParts = _parameterPropertyPath.Split('.');
            _propertyPathCache = new PropertyInfo[pathParts.Length];

            Type currentType = _viewModel.GetType();
            for (int i = 0; i < pathParts.Length; i++)
            {
                _propertyPathCache[i] = currentType.GetProperty(pathParts[i]);
                if (_propertyPathCache[i] == null)
                {
                    Debug.LogError($"CommandBinding: PropertyPath '{_parameterPropertyPath}' 中的属性 '{pathParts[i]}' 未找到");
                    _propertyPathCache = null;
                    return;
                }
                currentType = _propertyPathCache[i].PropertyType;
            }
        }

        /// <summary>
        /// 根据UI组件类型订阅事件
        /// </summary>
        private void SubscribeToUIEvent()
        {
            // 优先使用标准UGUI组件事件
            if (_targetComponent is Button button)
            {
                button.onClick.AddListener(OnUIEvent);
            }
            else if (_targetComponent is Toggle toggle)
            {
                toggle.onValueChanged.AddListener(OnUIEvent);
            }
            else if (_targetComponent is InputField inputField)
            {
                inputField.onEndEdit.AddListener(OnUIEvent);
            }
            else if (_targetComponent is Slider slider)
            {
                slider.onValueChanged.AddListener(OnUIEvent);
            }
            else if (_targetComponent is Dropdown dropdown)
            {
                dropdown.onValueChanged.AddListener(OnUIEvent);
            }
            else
            {
                // 通用UnityEvent处理（通过反射查找的事件）
                if (_unityEvent != null)
                {
                    _unityEvent.AddListener(OnUIEvent);
                }
                else
                {
                    Debug.LogWarning($"CommandBinding: 不支持的目标组件类型 {_targetComponent.GetType().Name}");
                }
            }
        }

        /// <summary>
        /// 取消事件订阅
        /// </summary>
        private void UnsubscribeFromUIEvent()
        {
            if (_targetComponent is Button button)
            {
                button.onClick.RemoveListener(OnUIEvent);
            }
            else if (_targetComponent is Toggle toggle)
            {
                toggle.onValueChanged.RemoveListener(OnUIEvent);
            }
            else if (_targetComponent is InputField inputField)
            {
                inputField.onEndEdit.RemoveListener(OnUIEvent);
            }
            else if (_targetComponent is Slider slider)
            {
                slider.onValueChanged.RemoveListener(OnUIEvent);
            }
            else if (_targetComponent is Dropdown dropdown)
            {
                dropdown.onValueChanged.RemoveListener(OnUIEvent);
            }
            else if (_unityEvent != null)
            {
                _unityEvent.RemoveListener(OnUIEvent);
            }
        }
    }
}