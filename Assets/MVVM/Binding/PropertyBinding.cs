using System;
using System.ComponentModel;
using System.Reflection;
using MVVM.Binding.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using Component = UnityEngine.Component;

namespace MVVM.Binding
{
    public class PropertyBinding:MonoBehaviour,IPropertyBinding
    {
        [Header("绑定配置")]
        [SerializeField] private BindingMode _mode = BindingMode.OneWay;
        [SerializeField] private string _sourceProperty;
        [SerializeField] private string _targetProperty;

        [Header("数据源")]
        [SerializeField] private MonoBehaviour _viewModel;
        [SerializeField] private Component _targetComponent;

        [Header("转换器")]
        [SerializeField] private IValueConverter _converter;

        // 反射缓存
        private PropertyInfo _sourcePropertyInfo;
        private PropertyInfo _targetPropertyInfo;
        private INotifyPropertyChanged _notifySource;

        // IPropertyBinding接口实现
        public string SourceProperty => _sourceProperty;
        public string TargetProperty => _targetProperty;
        public BindingMode BindingMode => _mode;
        public IValueConverter Converter => _converter;

        private void Start()
        {
            if (ValidateBindings())
            {
                Bind();
            }
        }

        private void OnDestroy()
        {
            UnBind();
        }

        private bool ValidateBindings()
        {
            if (_viewModel == null)
            {
                Debug.LogError("View Model is null");
                return false;
            }

            if (_targetComponent == null)
            {
                Debug.LogError("Target Component is null");
                return false;
            }

            if (string.IsNullOrEmpty(_sourceProperty) || string.IsNullOrEmpty(_targetProperty))
            {
                Debug.LogError("Property is null or empty");
                return false;
            }
            _sourcePropertyInfo = _viewModel.GetType().GetProperty(_sourceProperty);
            if (_sourcePropertyInfo == null)
            {
                Debug.LogError("Property is null");
                return false;
            }
            _targetPropertyInfo = _viewModel.GetType().GetProperty(_targetProperty);
            if (_targetPropertyInfo == null)
            {
                Debug.LogError("Property is null");
                return false;
            }
            return true;
        }

        public void Bind()
        {
            _notifySource=_viewModel as INotifyPropertyChanged;
            if (_notifySource != null)
            {
                _notifySource.PropertyChanged += OnSourcePropertyChanged;
            }
            UpdateTarget();
            if (_mode == BindingMode.TwoWay || _mode == BindingMode.OneWayToSource)
            {
                SubscribeToUIEvents();
            }
            Debug.Log($"PropertyBinding: {_sourceProperty} → {_targetProperty} ({_mode})");
        }
        public void UnBind()
        {
            if (_notifySource != null)
            {
                _notifySource.PropertyChanged -= OnSourcePropertyChanged;
                _notifySource = null;
            }
            UnsubscribeFromUIEvents();
            _sourcePropertyInfo = null;
            _targetPropertyInfo = null;
            Debug.Log($"PropertyUnBinding: {_sourceProperty} → {_targetProperty}");
        }

        public void UpdateSource()
        {
            if (_mode == BindingMode.OneWay || _mode == BindingMode.OneTime)
                return;

            if (_targetPropertyInfo == null || _sourcePropertyInfo == null)
                return;
            try
            {
                var targetValue = _targetPropertyInfo.GetValue(_targetComponent);
                var sourceValue = _converter?.ConvertBack(targetValue, _sourcePropertyInfo.PropertyType) ?? targetValue;
                _sourcePropertyInfo.SetValue(_viewModel, sourceValue);
                Debug.Log($"PropertyBinding: UI→ViewModel Update {_targetProperty} → {_sourceProperty} = {sourceValue}");
            }catch(Exception ex)
            {
                Debug.LogError($"PropertyBinding: Update Source Property Failed - {ex.Message}", this);
            }
        }

        public void UpdateTarget()
        {
            if (_sourcePropertyInfo == null || _targetPropertyInfo == null)
                return;

            try
            {
                // 获取ViewModel当前值
                var sourceValue = _sourcePropertyInfo.GetValue(_viewModel);

                // 应用转换器（如果有）
                var targetValue = _converter?.Convert(sourceValue, _targetPropertyInfo.PropertyType) ?? sourceValue;

                // 设置UI属性值
                _targetPropertyInfo.SetValue(_targetComponent, targetValue);

                Debug.Log($"PropertyBinding: ViewModel→UI更新 {_sourceProperty} → {_targetProperty} = {targetValue}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"PropertyBinding: 更新目标属性失败 - {ex.Message}", this);
            }
        }

        private void SubscribeToUIEvents()
        {
            // InputField文本变更
            if (_targetComponent is InputField inputField)
            {
                inputField.onValueChanged.AddListener(OnInputFieldValueChanged);
            }
            // Toggle状态变更
            else if (_targetComponent is Toggle toggle)
            {
                toggle.onValueChanged.AddListener(OnToggleValueChanged);
            }
            // Slider值变更
            else if (_targetComponent is Slider slider)
            {
                slider.onValueChanged.AddListener(OnSliderValueChanged);
            }
            // Dropdown选择变更
            else if (_targetComponent is Dropdown dropdown)
            {
                dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            }
        }
        private void UnsubscribeFromUIEvents()
        {
            if (_targetComponent is InputField inputField)
            {
                inputField.onValueChanged.RemoveListener(OnInputFieldValueChanged);
            }
            else if (_targetComponent is Toggle toggle)
            {
                toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
            }
            else if (_targetComponent is Slider slider)
            {
                slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            }
            else if (_targetComponent is Dropdown dropdown)
            {
                dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            }
        }
        private void OnInputFieldValueChanged(string value) => UpdateSource();
        private void OnToggleValueChanged(bool value) => UpdateSource();
        private void OnSliderValueChanged(float value) => UpdateSource();
        private void OnDropdownValueChanged(int value) => UpdateSource();
        private void OnSourcePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == _sourceProperty)
            {
                UpdateTarget();
            }
        }
    }
}