using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using DevFlow.Models;
using DevFlow.Services;

namespace DevFlow.Controls;

/// <summary>
/// 端口控件
/// 用于显示节点上的输入/输出/错误端口
/// 支持悬停效果和拖拽创建连线
/// </summary>
public class PortControl : Border
{
    #region 依赖属性定义

    public static readonly StyledProperty<DevicePort> PortProperty =
        AvaloniaProperty.Register<PortControl, DevicePort>(nameof(Port));

    public static readonly StyledProperty<DeviceNode> NodeProperty =
        AvaloniaProperty.Register<PortControl, DeviceNode>(nameof(Node));

    public static readonly StyledProperty<PortDirection> DirectionProperty =
        AvaloniaProperty.Register<PortControl, PortDirection>(nameof(Direction));

    public static readonly StyledProperty<bool> IsConnectedProperty =
        AvaloniaProperty.Register<PortControl, bool>(nameof(IsConnected));

    #endregion

    #region 属性访问器

    public DevicePort Port
    {
        get => GetValue(PortProperty);
        set => SetValue(PortProperty, value);
    }

    public DeviceNode Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public PortDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public bool IsConnected
    {
        get => GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    /// <summary>
    /// 是否被拖拽端点悬停（用于高亮显示）
    /// </summary>
    public bool IsHoveredByDrag
    {
        get => _isHoveredByDrag;
        set
        {
            if (_isHoveredByDrag != value)
            {
                _isHoveredByDrag = value;
                UpdateVisualState();
            }
        }
    }
    private bool _isHoveredByDrag;

    #endregion

    #region 事件定义

    public event EventHandler<PortDragStartedEventArgs>? DragStarted;
    public event EventHandler<PortDragEventArgs>? Dragging;
    public event EventHandler<PortDragEventArgs>? DragCompleted;
    public event EventHandler? DragCanceled;

    #endregion

    #region 私有字段

    private readonly Border _innerCircle;
    private bool _isHovered;
    private bool _isDragging;

    #endregion

    #region 构造函数

    public PortControl()
    {
        Width = 14;
        Height = 14;
        CornerRadius = new CornerRadius(7);
        BorderThickness = new Thickness(2);
        Cursor = Cursor.Parse("Hand");
        
        _innerCircle = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        
        Child = _innerCircle;
        
        UpdateVisualState();
        
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    #endregion

    #region 视觉状态管理

    private void UpdateVisualState()
    {
        var baseColor = Direction switch
        {
            PortDirection.Input => Color.Parse("#4CAF50"),
            PortDirection.Output => Color.Parse("#2196F3"),
            PortDirection.Error => Color.Parse("#F44336"),
            _ => Color.Parse("#888888")
        };

        // 始终保持端口大小不变
        Width = 14;
        Height = 14;
        CornerRadius = new CornerRadius(7);

        // 被拖拽端点悬停时，高亮效果：加粗边框 + 内部填充颜色
        if (IsHoveredByDrag)
        {
            Background = new SolidColorBrush(baseColor);  // 内部填充颜色
            BorderBrush = Brushes.White;
            BorderThickness = new Thickness(3);  // 加粗边框
            _innerCircle.Width = 8;  // 内圈稍大
            _innerCircle.Height = 8;
            _innerCircle.CornerRadius = new CornerRadius(4);
            _innerCircle.Background = Brushes.White;
        }
        // 普通悬停或拖拽时
        else if (_isHovered || _isDragging)
        {
            Background = new SolidColorBrush(baseColor);
            BorderBrush = Brushes.White;
            BorderThickness = new Thickness(2);
            _innerCircle.Width = 6;
            _innerCircle.Height = 6;
            _innerCircle.CornerRadius = new CornerRadius(3);
            _innerCircle.Background = Brushes.White;
        }
        else
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
            BorderBrush = new SolidColorBrush(baseColor);
            BorderThickness = new Thickness(2);
            _innerCircle.Width = 6;
            _innerCircle.Height = 6;
            _innerCircle.CornerRadius = new CornerRadius(3);
            _innerCircle.Background = IsConnected 
                ? new SolidColorBrush(baseColor)
                : new SolidColorBrush(Color.Parse("#333333"));
        }
    }

    #endregion

    #region 属性变化处理

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DirectionProperty || 
            change.Property == IsConnectedProperty)
        {
            UpdateVisualState();
        }
        
        if (change.Property == PortProperty && change.NewValue is DevicePort port)
        {
            ToolTip.SetTip(this, $"{port.Name} ({port.DataType.Name})");
            Direction = port.Direction;
        }
    }

    #endregion

    #region 鼠标事件处理

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isHovered = true;
        UpdateVisualState();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isHovered = false;
        UpdateVisualState();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Direction == PortDirection.Input && IsConnected)
        {
            return;
        }

        var flowEditor = this.FindAncestorOfType<FlowEditor>();
        if (flowEditor != null && flowEditor.SelectedConnection != null)
        {
            return;
        }

        e.Pointer.Capture(this);
        _isDragging = true;
        UpdateVisualState();
        
        var position = e.GetPosition(this);
        
        LogHelper.LogInfo("PortControl", "端口拖拽开始: Node={NodeTitle}, Port={PortName}, Direction={Direction}", 
            Node?.Title, Port?.Name, Direction);
        
        DragStarted?.Invoke(this, new PortDragStartedEventArgs
        {
            Port = Port,
            Node = Node,
            Direction = Direction,
            StartPosition = position
        });
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;

        var flowEditor = this.FindAncestorOfType<FlowEditor>();
        var position = flowEditor != null ? e.GetPosition(flowEditor) : e.GetPosition(this);
        
        Dragging?.Invoke(this, new PortDragEventArgs
        {
            Port = Port,
            Node = Node,
            Direction = Direction,
            CurrentPosition = position
        });
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;

        e.Pointer.Capture(null);
        _isDragging = false;
        UpdateVisualState();
        
        var flowEditor = this.FindAncestorOfType<FlowEditor>();
        var position = flowEditor != null ? e.GetPosition(flowEditor) : e.GetPosition(this);
        
        LogHelper.LogInfo("PortControl", "端口拖拽结束: Node={NodeTitle}, Port={PortName}, Position=({X},{Y})", 
            Node?.Title, Port?.Name, position.X, position.Y);
        
        DragCompleted?.Invoke(this, new PortDragEventArgs
        {
            Port = Port,
            Node = Node,
            Direction = Direction,
            CurrentPosition = position
        });
    }

    #endregion
}

#region 事件参数类

public class PortDragStartedEventArgs : EventArgs
{
    public DevicePort Port { get; set; } = null!;
    public DeviceNode Node { get; set; } = null!;
    public PortDirection Direction { get; set; }
    public Point StartPosition { get; set; }
    public bool Handled { get; set; }
}

public class PortDragEventArgs : EventArgs
{
    public DevicePort Port { get; set; } = null!;
    public DeviceNode Node { get; set; } = null!;
    public PortDirection Direction { get; set; }
    public Point CurrentPosition { get; set; }
    public bool Handled { get; set; }
}

#endregion
