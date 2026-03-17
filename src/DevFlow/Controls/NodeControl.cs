using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DevFlow.Models;
using DevFlow.Services;

namespace DevFlow.Controls;

/// <summary>
/// 节点控件
/// 包含标题栏、输入端口、输出端口、错误端口
/// </summary>
public class NodeControl : Border
{
    #region 常量定义

    public const double DefaultWidth = 180;
    public const double DefaultHeight = 130;

    #endregion

    #region 事件定义

    public event EventHandler<PortDragStartedEventArgs>? PortDragStarted;
    public event EventHandler<PortDragEventArgs>? PortDragging;
    public event EventHandler<PortDragEventArgs>? PortDragCompleted;
    public event EventHandler<PointerPressedEventArgs>? HeaderPressed;

    #endregion

    #region UI 组件

    private readonly Grid _root = new();
    private readonly StackPanel _inputPorts = new();
    private readonly StackPanel _outputPorts = new();
    private readonly StackPanel _errorPorts = new();
    private readonly TextBlock _titleText = new();
    private readonly TextBlock _deviceTypeText = new();
    private readonly Border _headerBorder = new();
    private readonly Border _contentArea = new();
    private readonly List<PortControl> _portControls = new();

    #endregion

    #region 构造函数

    public NodeControl()
    {
        Width = DefaultWidth;
        Height = DefaultHeight;
        
        Background = new SolidColorBrush(Color.Parse("#2D2D30"));
        BorderBrush = new SolidColorBrush(Color.Parse("#3E3E42"));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(0);

        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        CreateHeader();
        CreateContent();
        CreateFooter();

        Child = _root;
        IsHitTestVisible = true;
    }

    private void CreateHeader()
    {
        _headerBorder.Background = new SolidColorBrush(Color.Parse("#3E3E42"));
        _headerBorder.Padding = new Thickness(8, 4);
        _headerBorder.CornerRadius = new CornerRadius(4, 4, 0, 0);
        _headerBorder.Cursor = Cursor.Parse("SizeAll");
        
        var headerPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        _titleText.FontWeight = FontWeight.SemiBold;
        _titleText.FontSize = 12;
        _titleText.Foreground = Brushes.White;
        _titleText.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        headerPanel.Children.Add(_titleText);
        _headerBorder.Child = headerPanel;
        
        _headerBorder.PointerPressed += OnHeaderPointerPressed;
        
        Grid.SetRow(_headerBorder, 0);
        _root.Children.Add(_headerBorder);
    }

    private void CreateContent()
    {
        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        _inputPorts.Spacing = 4;
        _inputPorts.Margin = new Thickness(0, 0, 4, 0);
        Grid.SetColumn(_inputPorts, 0);
        contentGrid.Children.Add(_inputPorts);

        _contentArea.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        _contentArea.Padding = new Thickness(4);
        Grid.SetColumn(_contentArea, 1);
        contentGrid.Children.Add(_contentArea);

        var rightPorts = new StackPanel { Spacing = 4, Margin = new Thickness(4, 0, 0, 0) };
        _outputPorts.Spacing = 4;
        rightPorts.Children.Add(_outputPorts);
        _errorPorts.Spacing = 4;
        rightPorts.Children.Add(_errorPorts);
        Grid.SetColumn(rightPorts, 2);
        contentGrid.Children.Add(rightPorts);

        Grid.SetRow(contentGrid, 1);
        _root.Children.Add(contentGrid);
    }

    private void CreateFooter()
    {
        var footerBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Padding = new Thickness(8, 2),
            CornerRadius = new CornerRadius(0, 0, 4, 4)
        };
        _deviceTypeText.FontSize = 10;
        _deviceTypeText.Foreground = new SolidColorBrush(Color.Parse("#888888"));
        footerBorder.Child = _deviceTypeText;
        Grid.SetRow(footerBorder, 2);
        _root.Children.Add(footerBorder);
    }

    #endregion

    #region 事件处理

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HeaderPressed?.Invoke(this, e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is DeviceNode node)
        {
            _titleText.Text = node.Title;
            _deviceTypeText.Text = node.DeviceType?.Name ?? "";

            _inputPorts.Children.Clear();
            _outputPorts.Children.Clear();
            _errorPorts.Children.Clear();
            _portControls.Clear();

            if (node.DeviceType != null)
            {
                foreach (var port in node.DeviceType.InputPorts)
                {
                    var portControl = CreatePortControl(port, node);
                    _inputPorts.Children.Add(portControl);
                    _portControls.Add(portControl);
                }

                foreach (var port in node.DeviceType.OutputPorts)
                {
                    var portControl = CreatePortControl(port, node);
                    _outputPorts.Children.Add(portControl);
                    _portControls.Add(portControl);
                }

                foreach (var port in node.DeviceType.ErrorPorts)
                {
                    var portControl = CreatePortControl(port, node);
                    _errorPorts.Children.Add(portControl);
                    _portControls.Add(portControl);
                }
            }
            
            UpdateVisualState(node);
        }
    }

    #endregion

    #region 端口管理

    private PortControl CreatePortControl(DevicePort port, DeviceNode node)
    {
        var portControl = new PortControl
        {
            Port = port,
            Node = node,
            Direction = port.Direction,
            Margin = new Thickness(0, 2)
        };

        portControl.DragStarted += (s, e) => 
        {
            e.Handled = true;
            PortDragStarted?.Invoke(this, e);
        };
        portControl.Dragging += (s, e) => 
        {
            e.Handled = true;
            PortDragging?.Invoke(this, e);
        };
        portControl.DragCompleted += (s, e) => 
        {
            e.Handled = true;
            PortDragCompleted?.Invoke(this, e);
        };

        return portControl;
    }

    public PortControl? GetPortControl(string portName)
    {
        return _portControls.Find(p => p.Port?.Name == portName);
    }

    /// <summary>
    /// 获取端口在节点内的相对 Y 坐标（未缩放）
    /// </summary>
    public double GetPortRelativeY(string portName)
    {
        var portControl = GetPortControl(portName);
        if (portControl == null) return 28;
        
        int portIndex;
        if (portControl.Direction == PortDirection.Input)
        {
            portIndex = _inputPorts.Children.IndexOf(portControl);
        }
        else if (portControl.Direction == PortDirection.Output)
        {
            portIndex = _outputPorts.Children.IndexOf(portControl);
        }
        else
        {
            var outputCount = _outputPorts.Children.Count;
            var errorIndex = _errorPorts.Children.IndexOf(portControl);
            portIndex = outputCount + errorIndex;
        }
        
        if (portIndex < 0) portIndex = 0;
        
        const double headerHeight = 28;
        const double portHeight = 20;
        const double portSpacing = 4;
        const double marginTop = 4;
        
        return headerHeight + marginTop + portIndex * (portHeight + portSpacing) + portHeight / 2;
    }

    #endregion

    #region 视觉状态管理

    private void UpdateVisualState(DeviceNode node)
    {
        if (node.IsExecuting)
        {
            _headerBorder.Background = new SolidColorBrush(Color.Parse("#FFC107"));
            BorderBrush = new SolidColorBrush(Color.Parse("#FFC107"));
        }
        else if (node.HasError)
        {
            _headerBorder.Background = new SolidColorBrush(Color.Parse("#F44336"));
            BorderBrush = new SolidColorBrush(Color.Parse("#F44336"));
        }
        else if (node.IsCompleted)
        {
            _headerBorder.Background = new SolidColorBrush(Color.Parse("#4CAF50"));
            BorderBrush = new SolidColorBrush(Color.Parse("#4CAF50"));
        }
        else
        {
            _headerBorder.Background = new SolidColorBrush(Color.Parse("#3E3E42"));
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E42"));
        }
    }

    #endregion
}
