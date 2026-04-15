# CommandBinding命令绑定设计

## 概述
CommandBinding用于将UI组件的事件绑定到ViewModel的命令，支持ICommand接口和普通方法调用，自动处理命令可用性状态同步到UI交互状态。

## 核心需求
1. **绑定目标**：Button、Toggle、InputField等UGUI组件的事件
2. **命令源**：ViewModel的ICommand属性或普通方法
3. **参数传递**：支持固定值、属性路径绑定、事件参数转换
4. **状态同步**：根据CanExecute自动更新UI.interactable状态
5. **生命周期**：自动管理事件订阅和清理

## 类结构设计

### 序列化字段
```csharp
[Header("绑定配置")]
[SerializeField] private MonoBehaviour _viewModel;
[SerializeField] private string _commandName;        // 命令属性或方法名
[SerializeField] private string _eventName = "onClick"; // UI事件名
[SerializeField] private Component _targetComponent; // UI组件

[Header("命令参数")]
[SerializeField] private BindingParameterType _parameterType = BindingParameterType.None;
[SerializeField] private string _parameterPropertyPath; // 参数属性路径
[SerializeField] private string _parameterValue;        // 固定参数值
```

### 反射缓存
```csharp
private ICommand _command;              // ICommand实例缓存
private MethodInfo _methodInfo;         // 普通方法缓存
private PropertyInfo _propertyInfo;     // 属性缓存（返回ICommand）
private UnityEvent _unityEvent;         // UI事件引用
```

### 支持的事件类型
- `Button.onClick`
- `Toggle.onValueChanged`
- `InputField.onEndEdit`
- `Slider.onValueChanged`
- `Dropdown.onValueChanged`
- 自定义UnityEvent

## 关键方法实现

### 1. Bind() - 建立绑定
1. 验证ViewModel、目标组件、命令名称
2. 通过反射获取命令（ICommand属性或普通方法）
3. 获取UI组件上的目标事件（UnityEvent）
4. 订阅事件到命令执行处理器
5. 监听CanExecuteChanged事件（如果是ICommand）
6. 初始更新UI交互状态

### 2. UnBind() - 解除绑定
1. 取消事件订阅
2. 取消CanExecuteChanged监听
3. 清理缓存引用

### 3. UpdateTarget() - 更新UI状态
根据命令的CanExecute结果更新UI组件的interactable状态。

### 4. UpdateSource() - 触发命令
从ViewModel到UI的方向不适用，保留空实现。

## 参数处理设计

### BindingParameterType枚举
```csharp
public enum BindingParameterType
{
    None,           // 无参数
    FixedValue,     // 固定值（字符串，需类型转换）
    PropertyPath,   // 属性路径（从ViewModel获取）
    EventArgument   // 使用事件参数（如InputField.text）
}
```

### 参数值获取逻辑
1. **None**：传递null
2. **FixedValue**：将字符串转换为命令参数类型
3. **PropertyPath**：从ViewModel反射获取属性值
4. **EventArgument**：使用UI事件传递的参数

## 事件到命令的适配器

### 事件处理器方法
```csharp
private void OnUIEvent()
{
    object parameter = GetCommandParameter();
    if (_command != null)
    {
        if (_command.CanExecute(parameter))
            _command.Execute(parameter);
    }
    else if (_methodInfo != null)
    {
        _methodInfo.Invoke(_viewModel, new object[] { parameter });
    }
}
```

### CanExecuteChanged处理
```csharp
private void OnCanExecuteChanged(object sender, EventArgs e)
{
    UpdateTarget(); // 更新UI交互状态
}
```

## 类型转换支持
- 字符串到基本类型（int、float、bool、enum）
- 使用ValueConverter进行复杂类型转换
- 支持自定义参数转换器

## 错误处理
1. 反射失败时记录详细错误信息
2. 事件不存在时友好提示
3. 参数类型转换失败处理
4. 命令执行异常捕获和日志记录

## 使用示例
```csharp
// 场景设置：
// - ViewModel: PlayerViewModel (有AttackCommand属性，类型ICommand)
// - UI: Button组件
// - CommandBinding组件附加到Button

// 配置：
// ViewModel: PlayerViewModel引用
// CommandName: "AttackCommand"
// EventName: "onClick"
// ParameterType: FixedValue
// ParameterValue: "Enemy_001"
```

## 与PropertyBinding的协同
1. **数据绑定**：PropertyBinding处理属性值同步
2. **命令绑定**：CommandBinding处理用户交互
3. **组合使用**：按钮状态由PropertyBinding控制显示文本，CommandBinding控制点击行为

## 扩展性考虑
1. **自定义事件绑定**：支持任意UnityEvent
2. **异步命令支持**：AsyncRelayCommand集成
3. **复合命令**：支持多命令同时执行
4. **命令参数绑定**：动态参数源（其他UI组件值）