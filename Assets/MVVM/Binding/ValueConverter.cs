using System;
using MVVM.Binding.Interfaces;
using UnityEngine;

namespace MVVM.Binding
{
    /// <summary>
    /// 值转换器类型枚举
    /// </summary>
    public enum ConversionType
    {
        /// <summary>整数转字符串</summary>
        IntToString,
        /// <summary>浮点数转字符串</summary>
        FloatToString,
        /// <summary>布尔值转可见性数值</summary>
        BoolToVisibility,
        /// <summary>布尔值转字符串</summary>
        BoolToString,
        /// <summary>枚举值转字符串</summary>
        EnumToText,
        /// <summary>百分比转值（0-1 → 0-100）</summary>
        PercentToValue,
        /// <summary>值转百分比（0-100 → 0-1）</summary>
        ValueToPercent,
        /// <summary>自定义转换逻辑</summary>
        Custom
    }

    /// <summary>
    /// 对称设计的值转换器 - 所有转换方法都接收类型参数
    /// </summary>
    [CreateAssetMenu(fileName = "ValueConverter", menuName = "MVVM/Value Converter")]
    public class ValueConverter : ScriptableObject, IValueConverter
    {
        [Header("转换类型")]
        [SerializeField] private ConversionType _conversionType = ConversionType.IntToString;

        [Header("自定义格式")]
        [SerializeField] private string _formatString = "";

        [Header("布尔值映射")]
        [SerializeField] private string _trueText = "True";
        [SerializeField] private string _falseText = "False";

        [Header("可见性映射")]
        [SerializeField] private float _visibleValue = 1f;
        [SerializeField] private float _hiddenValue = 0f;

        #region IValueConverter接口实现

        public object Convert(object value, Type targetType)
        {
            if (value == null) return GetDefaultValue(targetType);

            try
            {
                return _conversionType switch
                {
                    ConversionType.IntToString => ConvertIntToString(value, targetType),
                    ConversionType.FloatToString => ConvertFloatToString(value, targetType),
                    ConversionType.BoolToVisibility => ConvertBoolToVisibility(value, targetType),
                    ConversionType.BoolToString => ConvertBoolToString(value, targetType),
                    ConversionType.EnumToText => ConvertEnumToText(value, targetType),
                    ConversionType.PercentToValue => ConvertPercentToValue(value, targetType),
                    ConversionType.ValueToPercent => ConvertValueToPercent(value, targetType),
                    ConversionType.Custom => HandleCustomConvert(value, targetType),
                    _ => throw new NotSupportedException($"不支持的转换类型: {_conversionType}")
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"值转换器正向转换失败: {ex.Message}");
                return GetDefaultValue(targetType);
            }
        }

        public object ConvertBack(object value, Type sourceType)
        {
            if (value == null) return GetDefaultValue(sourceType);

            try
            {
                return _conversionType switch
                {
                    ConversionType.IntToString => ConvertBackStringToInt(value, sourceType),
                    ConversionType.FloatToString => ConvertBackStringToFloat(value, sourceType),
                    ConversionType.BoolToVisibility => ConvertBackVisibilityToBool(value, sourceType),
                    ConversionType.BoolToString => ConvertBackStringToBool(value, sourceType),
                    ConversionType.EnumToText => ConvertBackStringToEnum(value, sourceType),
                    ConversionType.PercentToValue => ConvertBackValueToPercent(value, sourceType),
                    ConversionType.ValueToPercent => ConvertBackPercentToValue(value, sourceType),
                    ConversionType.Custom => HandleCustomConvertBack(value, sourceType),
                    _ => throw new NotSupportedException($"不支持的转换类型: {_conversionType}")
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"值转换器反向转换失败: {ex.Message}");
                return GetDefaultValue(sourceType);
            }
        }

        #endregion

        #region 正向转换方法

        private object ConvertIntToString(object value, Type targetType)
        {
            // 验证目标类型
            if (targetType != typeof(string))
                throw new ArgumentException($"IntToString转换器要求目标类型为string，实际为{targetType.Name}");

            if (value is int intValue)
            {
                return string.IsNullOrEmpty(_formatString)
                    ? intValue.ToString()
                    : intValue.ToString(_formatString);
            }

            // 尝试转换其他整数类型
            try
            {
                var intVal = System.Convert.ToInt32(value);
                return string.IsNullOrEmpty(_formatString)
                    ? intVal.ToString()
                    : intVal.ToString(_formatString);
            }
            catch
            {
                throw new ArgumentException($"IntToString转换器要求整数类型，实际为{value.GetType().Name}");
            }
        }

        private object ConvertFloatToString(object value, Type targetType)
        {
            // 验证目标类型
            if (targetType != typeof(string))
                throw new ArgumentException($"FloatToString转换器要求目标类型为string，实际为{targetType.Name}");

            if (value is float floatValue)
            {
                return string.IsNullOrEmpty(_formatString)
                    ? floatValue.ToString("F2")
                    : floatValue.ToString(_formatString);
            }

            if (value is double doubleValue)
            {
                return string.IsNullOrEmpty(_formatString)
                    ? doubleValue.ToString("F2")
                    : doubleValue.ToString(_formatString);
            }

            // 尝试转换其他浮点类型
            try
            {
                var doubleVal = System.Convert.ToDouble(value);
                return string.IsNullOrEmpty(_formatString)
                    ? doubleVal.ToString("F2")
                    : doubleVal.ToString(_formatString);
            }
            catch
            {
                throw new ArgumentException($"FloatToString转换器要求浮点类型，实际为{value.GetType().Name}");
            }
        }

        private object ConvertBoolToVisibility(object value, Type targetType)
        {
            // 验证目标类型为数值类型
            if (targetType != typeof(float) && targetType != typeof(double) && targetType != typeof(int))
                throw new ArgumentException($"BoolToVisibility转换器要求目标类型为float/double/int，实际为{targetType.Name}");

            if (value is bool boolValue)
            {
                var resultValue = boolValue ? _visibleValue : _hiddenValue;

                // 根据目标类型转换
                if (targetType == typeof(float))
                    return (float)resultValue;
                else if (targetType == typeof(double))
                    return (double)resultValue;
                else if (targetType == typeof(int))
                    return (int)Math.Round(resultValue);
            }

            throw new ArgumentException($"BoolToVisibility转换器要求bool类型，实际为{value.GetType().Name}");
        }

        private object ConvertBoolToString(object value, Type targetType)
        {
            // 验证目标类型
            if (targetType != typeof(string))
                throw new ArgumentException($"BoolToString转换器要求目标类型为string，实际为{targetType.Name}");

            if (value is bool boolValue)
            {
                return boolValue ? _trueText : _falseText;
            }

            throw new ArgumentException($"BoolToString转换器要求bool类型，实际为{value.GetType().Name}");
        }

        private object ConvertEnumToText(object value, Type targetType)
        {
            // 验证目标类型
            if (targetType != typeof(string))
                throw new ArgumentException($"EnumToText转换器要求目标类型为string，实际为{targetType.Name}");

            if (value == null)
                return string.Empty;

            if (!value.GetType().IsEnum)
                throw new ArgumentException($"EnumToText转换器要求枚举类型，实际为{value.GetType().Name}");

            return value.ToString();
        }

        private object ConvertPercentToValue(object value, Type targetType)
        {
            // 验证目标类型为数值类型
            if (targetType != typeof(float) && targetType != typeof(double) && targetType != typeof(int))
                throw new ArgumentException($"PercentToValue转换器要求目标类型为float/double/int，实际为{targetType.Name}");

            double percentValue;

            if (value is float floatValue)
                percentValue = floatValue;
            else if (value is double doubleValue)
                percentValue = doubleValue;
            else
            {
                try
                {
                    percentValue = System.Convert.ToDouble(value);
                }
                catch
                {
                    throw new ArgumentException($"PercentToValue转换器要求数值类型，实际为{value.GetType().Name}");
                }
            }

            var resultValue = percentValue * 100.0;

            // 根据目标类型转换
            if (targetType == typeof(float))
                return (float)resultValue;
            else if (targetType == typeof(double))
                return resultValue;
            else // int
                return (int)Math.Round(resultValue);
        }

        private object ConvertValueToPercent(object value, Type targetType)
        {
            // 验证目标类型为数值类型
            if (targetType != typeof(float) && targetType != typeof(double))
                throw new ArgumentException($"ValueToPercent转换器要求目标类型为float/double，实际为{targetType.Name}");

            double inputValue;

            if (value is float floatValue)
                inputValue = floatValue;
            else if (value is double doubleValue)
                inputValue = doubleValue;
            else if (value is int intValue)
                inputValue = intValue;
            else
            {
                try
                {
                    inputValue = System.Convert.ToDouble(value);
                }
                catch
                {
                    throw new ArgumentException($"ValueToPercent转换器要求数值类型，实际为{value.GetType().Name}");
                }
            }

            var resultValue = inputValue / 100.0;

            // 根据目标类型转换
            if (targetType == typeof(float))
                return (float)resultValue;
            else // double
                return resultValue;
        }

        #endregion

        #region 反向转换方法（对称设计，都接收类型参数）

        private object ConvertBackStringToInt(object value, Type sourceType)
        {
            // 验证源类型
            if (sourceType != typeof(int))
                throw new ArgumentException($"ConvertBackStringToInt转换器要求源类型为int，实际为{sourceType.Name}");

            if (value is string strValue)
            {
                if (int.TryParse(strValue, out int intResult))
                    return intResult;

                throw new ArgumentException($"无法将字符串 '{strValue}' 转换为int类型");
            }

            // 尝试转换其他类型
            try
            {
                return System.Convert.ToInt32(value);
            }
            catch
            {
                throw new ArgumentException($"ConvertBackStringToInt转换器要求string类型，实际为{value.GetType().Name}");
            }
        }

        private object ConvertBackStringToFloat(object value, Type sourceType)
        {
            // 验证源类型
            if (sourceType != typeof(float) && sourceType != typeof(double))
                throw new ArgumentException($"ConvertBackStringToFloat转换器要求源类型为float/double，实际为{sourceType.Name}");

            if (value is string strValue)
            {
                if (float.TryParse(strValue, out float floatResult))
                {
                    if (sourceType == typeof(float))
                        return floatResult;
                    else // double
                        return (double)floatResult;
                }

                throw new ArgumentException($"无法将字符串 '{strValue}' 转换为浮点类型");
            }

            // 尝试转换其他类型
            try
            {
                var doubleVal = System.Convert.ToDouble(value);
                if (sourceType == typeof(float))
                    return (float)doubleVal;
                else
                    return doubleVal;
            }
            catch
            {
                throw new ArgumentException($"ConvertBackStringToFloat转换器要求string类型，实际为{value.GetType().Name}");
            }
        }

        private object ConvertBackVisibilityToBool(object value, Type sourceType)
        {
            // 验证源类型
            if (sourceType != typeof(bool))
                throw new ArgumentException($"ConvertBackVisibilityToBool转换器要求源类型为bool，实际为{sourceType.Name}");

            double numericValue;

            if (value is float floatValue)
                numericValue = floatValue;
            else if (value is double doubleValue)
                numericValue = doubleValue;
            else if (value is int intValue)
                numericValue = intValue;
            else
            {
                try
                {
                    numericValue = System.Convert.ToDouble(value);
                }
                catch
                {
                    throw new ArgumentException($"ConvertBackVisibilityToBool转换器要求数值类型，实际为{value.GetType().Name}");
                }
            }

            // 使用阈值判断（接近_visibleValue则为true）
            var threshold = (_visibleValue + _hiddenValue) / 2.0;
            return numericValue >= threshold;
        }

        private object ConvertBackStringToBool(object value, Type sourceType)
        {
            // 验证源类型
            if (sourceType != typeof(bool))
                throw new ArgumentException($"ConvertBackStringToBool转换器要求源类型为bool，实际为{sourceType.Name}");

            if (value is string strValue)
            {
                // 检查是否匹配true/false文本
                if (strValue.Equals(_trueText, StringComparison.OrdinalIgnoreCase))
                    return true;
                else if (strValue.Equals(_falseText, StringComparison.OrdinalIgnoreCase))
                    return false;

                // 尝试解析为布尔值
                if (bool.TryParse(strValue, out bool boolResult))
                    return boolResult;

                throw new ArgumentException($"无法将字符串 '{strValue}' 转换为bool类型");
            }

            // 尝试转换其他类型
            try
            {
                return System.Convert.ToBoolean(value);
            }
            catch
            {
                throw new ArgumentException($"ConvertBackStringToBool转换器要求string类型，实际为{value.GetType().Name}");
            }
        }

        private object ConvertBackStringToEnum(object value, Type sourceType)
        {
            // 验证源类型是枚举
            if (!sourceType.IsEnum)
                throw new ArgumentException($"ConvertBackStringToEnum转换器要求枚举类型，实际为{sourceType.Name}");

            if (value is string strValue)
            {
                try
                {
                    return Enum.Parse(sourceType, strValue);
                }
                catch
                {
                    throw new ArgumentException($"无法将字符串 '{strValue}' 转换为枚举类型 {sourceType.Name}");
                }
            }

            throw new ArgumentException($"ConvertBackStringToEnum转换器要求string类型，实际为{value.GetType().Name}");
        }

        private object ConvertBackValueToPercent(object value, Type sourceType)
        {
            // 验证源类型为数值类型
            if (sourceType != typeof(float) && sourceType != typeof(double))
                throw new ArgumentException($"ConvertBackValueToPercent转换器要求源类型为float/double，实际为{sourceType.Name}");

            double inputValue;

            if (value is float floatValue)
                inputValue = floatValue;
            else if (value is double doubleValue)
                inputValue = doubleValue;
            else if (value is int intValue)
                inputValue = intValue;
            else
            {
                try
                {
                    inputValue = System.Convert.ToDouble(value);
                }
                catch
                {
                    throw new ArgumentException($"ConvertBackValueToPercent转换器要求数值类型，实际为{value.GetType().Name}");
                }
            }

            var resultValue = inputValue / 100.0;

            // 根据源类型转换
            if (sourceType == typeof(float))
                return (float)resultValue;
            else // double
                return resultValue;
        }

        private object ConvertBackPercentToValue(object value, Type sourceType)
        {
            // 验证源类型为数值类型
            if (sourceType != typeof(float) && sourceType != typeof(double) && sourceType != typeof(int))
                throw new ArgumentException($"ConvertBackPercentToValue转换器要求源类型为float/double/int，实际为{sourceType.Name}");

            double percentValue;

            if (value is float floatValue)
                percentValue = floatValue;
            else if (value is double doubleValue)
                percentValue = doubleValue;
            else
            {
                try
                {
                    percentValue = System.Convert.ToDouble(value);
                }
                catch
                {
                    throw new ArgumentException($"ConvertBackPercentToValue转换器要求数值类型，实际为{value.GetType().Name}");
                }
            }

            var resultValue = percentValue * 100.0;

            // 根据源类型转换
            if (sourceType == typeof(float))
                return (float)resultValue;
            else if (sourceType == typeof(double))
                return resultValue;
            else // int
                return (int)Math.Round(resultValue);
        }

        #endregion

        #region 自定义转换处理（供子类重写）

        protected virtual object HandleCustomConvert(object value, Type targetType)
        {
            // 基类不实现自定义转换，子类应重写此方法
            throw new NotImplementedException("自定义转换需要在子类中实现HandleCustomConvert方法");
        }

        protected virtual object HandleCustomConvertBack(object value, Type sourceType)
        {
            // 基类不实现自定义转换，子类应重写此方法
            throw new NotImplementedException("自定义转换需要在子类中实现HandleCustomConvertBack方法");
        }

        #endregion

        #region 辅助方法

        private object GetDefaultValue(Type type)
        {
            if (type == null) return null;

            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }

        /// <summary>
        /// 检查转换器是否支持特定类型组合
        /// </summary>
        public bool CanConvert(Type sourceType, Type targetType)
        {
            return _conversionType switch
            {
                ConversionType.IntToString => (sourceType == typeof(int) || sourceType == typeof(short) || sourceType == typeof(long)) && targetType == typeof(string),
                ConversionType.FloatToString => (sourceType == typeof(float) || sourceType == typeof(double) || sourceType == typeof(decimal)) && targetType == typeof(string),
                ConversionType.BoolToVisibility => sourceType == typeof(bool) && (targetType == typeof(float) || targetType == typeof(double) || targetType == typeof(int)),
                ConversionType.BoolToString => sourceType == typeof(bool) && targetType == typeof(string),
                ConversionType.EnumToText => sourceType.IsEnum && targetType == typeof(string),
                ConversionType.PercentToValue => (sourceType == typeof(float) || sourceType == typeof(double)) &&
                                                (targetType == typeof(float) || targetType == typeof(double) || targetType == typeof(int)),
                ConversionType.ValueToPercent => (sourceType == typeof(float) || sourceType == typeof(double) || sourceType == typeof(int)) &&
                                                (targetType == typeof(float) || targetType == typeof(double)),
                ConversionType.Custom => true, // 自定义转换器假设支持
                _ => false
            };
        }

        #endregion

        #region 编辑器方法

        /// <summary>
        /// 在编辑器中重置配置
        /// </summary>
        public void ResetToDefault()
        {
            _formatString = "";
            _trueText = "True";
            _falseText = "False";
            _visibleValue = 1f;
            _hiddenValue = 0f;
        }

        #endregion
    }
}