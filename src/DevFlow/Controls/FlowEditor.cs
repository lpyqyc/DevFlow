using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using DevFlow.Models;
using DevFlow.Services;
using DevFlow.ViewModels;

namespace DevFlow.Controls;

/// <summary>
/// 流程编辑器控件
/// </summary>
public class FlowEditor : Canvas
{
    #region 常量定义

    private const double NodeWidth = NodeControl.DefaultWidth;
    private const double NodeHeight = NodeControl.DefaultHeight;

    #endregion

    #region 依赖属性定义

    public static readonly StyledProperty<ObservableCollection<DeviceNode>> NodesProperty =
        AvaloniaProperty.Register<FlowEditor, ObservableCollection<DeviceNode>>(nameof(Nodes));

    public static readonly StyledProperty<ObservableCollection<ConnectionViewModel>> ConnectionsProperty =
        AvaloniaProperty.Register<FlowEditor, ObservableCollection<ConnectionViewModel>>(nameof(Connections));

    public static readonly StyledProperty<DeviceNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<FlowEditor, DeviceNode?>(nameof(SelectedNode));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<FlowEditor, double>(nameof(Zoom), 1.0);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<FlowEditor, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<bool> EnableAutoAlignProperty =
        AvaloniaProperty.Register<FlowEditor, bool>(nameof(EnableAutoAlign), true);

    public static readonly StyledProperty<double> GridSpacingProperty =
        AvaloniaProperty.Register<FlowEditor, double>(nameof(GridSpacing), 20.0);

    public static readonly StyledProperty<ConnectionViewModel?> SelectedConnectionProperty =
        AvaloniaProperty.Register<FlowEditor, ConnectionViewModel?>(nameof(SelectedConnection));

    #endregion

    #region 属性访问器

    public ObservableCollection<DeviceNode> Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public ObservableCollection<ConnectionViewModel> Connections
    {
        get => GetValue(ConnectionsProperty);
        set => SetValue(ConnectionsProperty, value);
    }

    public DeviceNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public ConnectionViewModel? SelectedConnection
    {
        get => GetValue(SelectedConnectionProperty);
        set => SetValue(SelectedConnectionProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set
        {
            SetValue(ZoomProperty, value);
            UpdateAllNodePositions();
            // 延迟更新连线，确保布局完成
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateAllConnections());
        }
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public bool EnableAutoAlign
    {
        get => GetValue(EnableAutoAlignProperty);
        set => SetValue(EnableAutoAlignProperty, value);
    }

    public double GridSpacing
    {
        get => GetValue(GridSpacingProperty);
        set => SetValue(GridSpacingProperty, value);
    }

    #endregion

    #region 事件定义

    public event EventHandler<ConnectionCreatedEventArgs>? ConnectionCreated;
    public event EventHandler<ConnectionDeletedEventArgs>? ConnectionDeleted;

    #endregion

    #region 私有字段

    private double _translateX;
    private double _translateY;
    private Point _lastMousePosition;
    private bool _isPanning;
    private DeviceNode? _draggingNode;
    private Point _dragStartPosition;
    private Point _nodeStartPosition;
    private readonly Dictionary<string, NodeControl> _nodeControls = new();
    private readonly Dictionary<string, ConnectionControl> _connectionControls = new();
    private readonly List<Line> _gridLines = new();
    
    private ConnectionControl? _previewConnection;
    private DeviceNode? _dragSourceNode;
    private string? _dragSourcePort;
    private PortDirection _dragPortDirection;

    public double TranslateX => _translateX;
    public double TranslateY => _translateY;

    private readonly IConnectionValidator _validator = new ConnectionValidator();
    
    private ConnectionViewModel? _draggingConnection;
    private bool _isDraggingStartEndpoint;

    #endregion

    #region 构造函数

    public FlowEditor()
    {
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        ClipToBounds = true;
        Focusable = true;

        AddHandler(PointerPressedEvent, OnPointerPressed);
        AddHandler(PointerMovedEvent, OnPointerMoved);
        AddHandler(PointerReleasedEvent, OnPointerReleased);
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble);
        AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        
        PointerPressed += (s, e) => Focus();
        
        PropertyChanged += (s, e) =>
        {
            if (e.Property == BoundsProperty)
            {
                UpdateGrid();
            }
        };
        
        // 监听布局更新完成
        LayoutUpdated += OnLayoutUpdated;
    }

    static FlowEditor()
    {
        NodesProperty.Changed.AddClassHandler<FlowEditor>((editor, e) => editor.OnNodesChanged(e));
        ConnectionsProperty.Changed.AddClassHandler<FlowEditor>((editor, e) => editor.OnConnectionsChanged(e));
    }

    #endregion

    #region 布局更新处理

    private bool _needsConnectionUpdate = false;

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_needsConnectionUpdate)
        {
            _needsConnectionUpdate = false;
            UpdateAllConnectionsInternal();
        }
    }

    private void ScheduleConnectionUpdate()
    {
        _needsConnectionUpdate = true;
    }

    #endregion

    #region 键盘事件处理

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            if (SelectedConnection != null)
            {
                DeleteConnection(SelectedConnection);
                e.Handled = true;
            }
        }
        
        if (e.Key == Key.Escape)
        {
            CancelPortDrag();
            e.Handled = true;
        }
    }

    #endregion

    #region 网格绘制

    private void UpdateGrid()
    {
        foreach (var line in _gridLines)
        {
            Children.Remove(line);
        }
        _gridLines.Clear();

        if (!ShowGrid) return;

        var spacing = GridSpacing * Zoom;
        var offsetX = _translateX % spacing;
        var offsetY = _translateY % spacing;
        var width = Bounds.Width;
        var height = Bounds.Height;

        for (double x = offsetX; x < width; x += spacing)
        {
            var line = new Line
            {
                StartPoint = new Point(x, 0),
                EndPoint = new Point(x, height),
                Stroke = new SolidColorBrush(Color.Parse("#333333")),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            _gridLines.Add(line);
            Children.Insert(0, line);
        }

        for (double y = offsetY; y < height; y += spacing)
        {
            var line = new Line
            {
                StartPoint = new Point(0, y),
                EndPoint = new Point(width, y),
                Stroke = new SolidColorBrush(Color.Parse("#333333")),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            _gridLines.Add(line);
            Children.Insert(0, line);
        }
    }

    #endregion

    #region 鼠标事件处理

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var position = e.GetCurrentPoint(this).Position;
        _lastMousePosition = position;

        if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed || 
            (e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            _isPanning = true;
            return;
        }

        var clickedNode = GetNodeAtPosition(position);
        if (clickedNode != null)
        {
            SelectedNode = clickedNode;
            // 取消所有连线选中
            ClearAllConnectionSelection();
        }
        else
        {
            SelectedNode = null;
            // 点击空白区域也取消连线选中
            ClearAllConnectionSelection();
        }
    }

    private void OnNodeHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is NodeControl nodeControl && nodeControl.DataContext is DeviceNode node)
        {
            var position = e.GetPosition(this);
            _lastMousePosition = position;
            
            SelectedNode = node;
            // 取消所有连线选中
            ClearAllConnectionSelection();
            _draggingNode = node;
            _nodeStartPosition = node.Position;
            _dragStartPosition = new Point(
                (position.X - _translateX) / Zoom,
                (position.Y - _translateY) / Zoom
            );
            
            e.Handled = true;
        }
    }
    
    /// <summary>
    /// 取消所有连线选中状态
    /// </summary>
    private void ClearAllConnectionSelection()
    {
        foreach (var connControl in _connectionControls.Values)
        {
            connControl.IsSelected = false;
        }
        SelectedConnection = null;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(this);

        if (_isPanning)
        {
            var delta = position - _lastMousePosition;
            _translateX += delta.X;
            _translateY += delta.Y;
            UpdateAllNodePositions();
            ScheduleConnectionUpdate();
            UpdateGrid();
            InvalidateVisual();
        }
        else if (_draggingNode != null)
        {
            var currentPos = new Point(
                (position.X - _translateX) / Zoom,
                (position.Y - _translateY) / Zoom
            );

            var delta = currentPos - _dragStartPosition;
            var newX = _nodeStartPosition.X + delta.X;
            var newY = _nodeStartPosition.Y + delta.Y;

            if (EnableAutoAlign)
            {
                newX = Math.Round(newX / GridSpacing) * GridSpacing;
                newY = Math.Round(newY / GridSpacing) * GridSpacing;
            }

            _draggingNode.Position = new Point(newX, newY);
            UpdateNodeControlPosition(_draggingNode);
            UpdateConnectionsForNode(_draggingNode.Id);
        }

        if (_previewConnection != null && _dragSourceNode != null)
        {
            var sourcePos = GetPortPositionActual(_dragSourceNode, _dragSourcePort!, _dragPortDirection);
            _previewConnection.StartPoint = sourcePos;
            _previewConnection.EndPoint = position;
            _previewConnection.InvalidateVisual();
        }
        
        if (_draggingConnection != null && _connectionControls.TryGetValue(_draggingConnection.Id, out var connControl))
        {
            if (_isDraggingStartEndpoint)
            {
                connControl.StartPoint = position;
            }
            else
            {
                connControl.EndPoint = position;
            }
            connControl.InvalidateVisual();
        }

        _lastMousePosition = position;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPanning = false;
        _draggingNode = null;
        
        if (_draggingConnection != null && _connectionControls.TryGetValue(_draggingConnection.Id, out var connControl))
        {
            var position = e.GetPosition(this);
            var targetNode = GetNodeAtPosition(position);
            
            if (targetNode != null)
            {
                var targetPort = FindPortAtPosition(targetNode, position);
                if (targetPort != null)
                {
                    if (_isDraggingStartEndpoint)
                    {
                        _draggingConnection.SourceNodeId = targetNode.Id;
                        _draggingConnection.SourcePort = targetPort.Name;
                        _draggingConnection.Direction = targetPort.Direction;
                    }
                    else
                    {
                        _draggingConnection.TargetNodeId = targetNode.Id;
                        _draggingConnection.TargetPort = targetPort.Name;
                    }
                    
                    UpdateConnectionPositionActual(connControl, 
                        Nodes.First(n => n.Id == _draggingConnection.SourceNodeId),
                        Nodes.First(n => n.Id == _draggingConnection.TargetNodeId),
                        _draggingConnection.SourcePort, _draggingConnection.TargetPort);
                }
            }
            
            _draggingConnection = null;
        }
    }

    #endregion

    #region 拖放事件处理

    private void OnDragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618
        if (e.Data.Contains("DeviceType"))
#pragma warning restore CS0618
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618
        var deviceTypeObj = e.Data.Get("DeviceType");
#pragma warning restore CS0618
        
        if (deviceTypeObj is DeviceTypeItem deviceType)
        {
            var position = e.GetPosition(this);
            
            var canvasX = (position.X - _translateX) / Zoom;
            var canvasY = (position.Y - _translateY) / Zoom;
            
            var nodeX = canvasX - NodeWidth / 2;
            var nodeY = canvasY - NodeHeight / 2;
            
            var node = new DeviceNode
            {
                Title = deviceType.Name,
                DeviceType = deviceType.Type,
                DeviceTypeId = deviceType.Type.Id,
                Position = new Point(nodeX, nodeY)
            };
            
            foreach (var port in deviceType.Type.InputPorts)
            {
                node.Inputs[port.Name] = port.DefaultValue;
            }
            foreach (var port in deviceType.Type.OutputPorts)
            {
                node.Outputs[port.Name] = null;
            }
            foreach (var port in deviceType.Type.ErrorPorts)
            {
                node.Errors[port.Name] = null;
            }
            
            if (Nodes != null)
            {
                Nodes.Add(node);
            }
        }
    }

    #endregion

    #region 鼠标滚轮事件处理

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var position = e.GetPosition(this);
        var zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
        var newZoom = Zoom * zoomFactor;

        newZoom = Math.Max(0.25, Math.Min(4.0, newZoom));

        var oldZoom = Zoom;
        
        var mouseX = (position.X - _translateX) / oldZoom;
        var mouseY = (position.Y - _translateY) / oldZoom;
        _translateX = position.X - mouseX * newZoom;
        _translateY = position.Y - mouseY * newZoom;

        Zoom = newZoom;

        UpdateAllNodePositions();
        ScheduleConnectionUpdate();
        UpdateGrid();
        
        LogHelper.LogInfo("FlowEditor", "缩放: Zoom={Zoom:F2}", newZoom);
    }

    #endregion

    #region 节点查找方法

    private DeviceNode? GetNodeAtPosition(Point position)
    {
        var canvasX = (position.X - _translateX) / Zoom;
        var canvasY = (position.Y - _translateY) / Zoom;

        foreach (var node in Nodes.Reverse())
        {
            if (canvasX >= node.Position.X && canvasX <= node.Position.X + NodeWidth &&
                canvasY >= node.Position.Y && canvasY <= node.Position.Y + NodeHeight)
            {
                return node;
            }
        }

        return null;
    }

    #endregion

    #region 节点管理方法

    public void AddNode(DeviceNode node)
    {
        if (_nodeControls.ContainsKey(node.Id)) return;
        
        var control = new NodeControl { DataContext = node };
        control.Tag = node;
        
        control.HeaderPressed += OnNodeHeaderPressed;
        control.PortDragStarted += OnPortDragStarted;
        control.PortDragging += OnPortDragging;
        control.PortDragCompleted += OnPortDragCompleted;
        
        node.PropertyChanged += OnNodePropertyChanged;
        
        SetLeft(control, node.Position.X * Zoom + _translateX);
        SetTop(control, node.Position.Y * Zoom + _translateY);
        control.Width = NodeWidth * Zoom;
        control.Height = NodeHeight * Zoom;
        
        _nodeControls[node.Id] = control;
        Children.Add(control);
        
        // 节点添加后，延迟更新相关连线
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateConnectionsForNode(node.Id);
        });
    }

    public void RemoveNode(DeviceNode node)
    {
        if (_nodeControls.TryGetValue(node.Id, out var control))
        {
            control.HeaderPressed -= OnNodeHeaderPressed;
            control.PortDragStarted -= OnPortDragStarted;
            control.PortDragging -= OnPortDragging;
            control.PortDragCompleted -= OnPortDragCompleted;
            
            node.PropertyChanged -= OnNodePropertyChanged;
            
            Children.Remove(control);
            _nodeControls.Remove(node.Id);
        }
    }
    
    private void OnNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is DeviceNode node && e.PropertyName == nameof(DeviceNode.Position))
        {
            UpdateNodeControlPosition(node);
            UpdateConnectionsForNode(node.Id);
        }
    }

    private void UpdateNodeControlPosition(DeviceNode node)
    {
        if (_nodeControls.TryGetValue(node.Id, out var control))
        {
            SetLeft(control, node.Position.X * Zoom + _translateX);
            SetTop(control, node.Position.Y * Zoom + _translateY);
            control.Width = NodeWidth * Zoom;
            control.Height = NodeHeight * Zoom;
        }
    }

    private void UpdateAllNodePositions()
    {
        foreach (var node in Nodes)
        {
            UpdateNodeControlPosition(node);
        }
    }

    #endregion

    #region 端口拖拽处理

    private void OnPortDragStarted(object? sender, PortDragStartedEventArgs e)
    {
        _draggingNode = null;
        _dragSourceNode = e.Node;
        _dragSourcePort = e.Port.Name;
        _dragPortDirection = e.Direction;
        _hoveredPort = null;
        
        var sourcePos = GetPortPositionActual(e.Node, e.Port.Name, e.Direction);
        
        _previewConnection = new ConnectionControl
        {
            StartPoint = sourcePos,
            EndPoint = sourcePos,
            ConnectionType = e.Direction,
            IsHitTestVisible = false
        };
        
        Children.Add(_previewConnection);
    }

    private void OnPortDragging(object? sender, PortDragEventArgs e)
    {
        if (_previewConnection != null && _dragSourceNode != null)
        {
            var sourcePos = GetPortPositionActual(_dragSourceNode, _dragSourcePort!, _dragPortDirection);
            _previewConnection.StartPoint = sourcePos;
            _previewConnection.EndPoint = e.CurrentPosition;
            _previewConnection.InvalidateVisual();
            
            // 检测悬停的端口并高亮
            var targetNode = GetNodeAtPosition(e.CurrentPosition);
            if (targetNode != null)
            {
                var targetPort = FindPortAtPosition(targetNode, e.CurrentPosition);
                if (targetPort != null && _nodeControls.TryGetValue(targetNode.Id, out var nodeControl))
                {
                    var portControl = nodeControl.GetPortControl(targetPort.Name);
                    if (portControl != null && portControl != _hoveredPort)
                    {
                        // 取消之前的高亮
                        if (_hoveredPort != null)
                        {
                            _hoveredPort.IsHoveredByDrag = false;
                        }
                        // 设置新高亮
                        _hoveredPort = portControl;
                        _hoveredPort.IsHoveredByDrag = true;
                    }
                }
                else if (_hoveredPort != null)
                {
                    _hoveredPort.IsHoveredByDrag = false;
                    _hoveredPort = null;
                }
            }
            else if (_hoveredPort != null)
            {
                _hoveredPort.IsHoveredByDrag = false;
                _hoveredPort = null;
            }
        }
    }

    private void OnPortDragCompleted(object? sender, PortDragEventArgs e)
    {
        // 清除端口高亮
        if (_hoveredPort != null)
        {
            _hoveredPort.IsHoveredByDrag = false;
            _hoveredPort = null;
        }
        
        if (_previewConnection != null)
        {
            Children.Remove(_previewConnection);
            _previewConnection = null;
        }
        
        var targetNode = GetNodeAtPosition(e.CurrentPosition);
        
        if (targetNode != null && targetNode != _dragSourceNode && _dragSourceNode != null && _dragSourcePort != null)
        {
            var targetPort = FindPortAtPosition(targetNode, e.CurrentPosition);
            
            if (targetPort != null)
            {
                // 验证连线规则
                bool validConnection = true;
                
                // 起点（拖拽源）只能是输出端口或错误端口
                if (_dragPortDirection == PortDirection.Input)
                {
                    LogHelper.LogWarning("FlowEditor", "输入端口不能作为连线起点");
                    validConnection = false;
                }
                
                // 终点（目标）只能是输入端口
                if (targetPort.Direction != PortDirection.Input)
                {
                    LogHelper.LogWarning("FlowEditor", "输出/错误端口不能作为连线终点");
                    validConnection = false;
                }
                
                if (validConnection)
                {
                    TryCreateConnection(_dragSourceNode, _dragSourcePort, targetNode, targetPort.Name);
                }
            }
        }
        
        _dragSourceNode = null;
        _dragSourcePort = null;
    }

    private void CancelPortDrag()
    {
        // 清除端口高亮
        if (_hoveredPort != null)
        {
            _hoveredPort.IsHoveredByDrag = false;
            _hoveredPort = null;
        }
        
        if (_previewConnection != null)
        {
            Children.Remove(_previewConnection);
            _previewConnection = null;
        }
        _dragSourceNode = null;
        _dragSourcePort = null;
    }

    #endregion

    #region 端口位置计算（使用 TransformToVisual 获取实际位置）

    /// <summary>
    /// 获取端口在屏幕坐标中的实际位置
    /// 使用 TransformToVisual 获取 PortControl 的实际渲染位置
    /// </summary>
    private Point GetPortPositionActual(DeviceNode node, string portName, PortDirection direction)
    {
        if (!_nodeControls.TryGetValue(node.Id, out var control))
        {
            LogHelper.LogWarning("FlowEditor", "GetPortPositionActual: 节点控件未找到 Node={NodeId}", node.Id);
            return new Point(0, 0);
        }
        
        var portControl = control.GetPortControl(portName);
        if (portControl == null)
        {
            LogHelper.LogWarning("FlowEditor", "GetPortPositionActual: 端口控件未找到 Port={PortName}", portName);
            return new Point(0, 0);
        }
        
        // 使用 TransformToVisual 获取端口相对于 FlowEditor 的实际位置
        try
        {
            var transform = portControl.TransformToVisual(this);
            if (transform != null)
            {
                var portBounds = portControl.Bounds;
                var transformedBounds = portBounds.TransformToAABB(transform.Value);
                
                // 返回端口中心点
                var portCenter = new Point(
                    transformedBounds.Left + transformedBounds.Width / 2,
                    transformedBounds.Top + transformedBounds.Height / 2
                );
                
                LogHelper.LogDebug("FlowEditor", "GetPortPositionActual: Node={Title}, Port={Port}, Center=({X:F1},{Y:F1})",
                    node.Title, portName, portCenter.X, portCenter.Y);
                
                return portCenter;
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError("FlowEditor", "GetPortPositionActual 异常: {Message}", ex.Message);
        }
        
        // 回退：使用手动计算
        return CalculatePortPositionFallback(node, portName, direction);
    }
    
    /// <summary>
    /// 回退方法：手动计算端口位置
    /// </summary>
    private Point CalculatePortPositionFallback(DeviceNode node, string portName, PortDirection direction)
    {
        var nodeScreenX = node.Position.X * Zoom + _translateX;
        var nodeScreenY = node.Position.Y * Zoom + _translateY;
        var nodeScreenW = NodeWidth * Zoom;
        
        var relativeY = GetPortRelativeY(node, portName, direction);
        var portScreenY = nodeScreenY + relativeY * Zoom;
        
        double portScreenX;
        if (direction == PortDirection.Input)
        {
            portScreenX = nodeScreenX;
        }
        else
        {
            portScreenX = nodeScreenX + nodeScreenW;
        }
        
        return new Point(portScreenX, portScreenY);
    }
    
    /// <summary>
    /// 获取端口在节点内的相对 Y 坐标
    /// </summary>
    private double GetPortRelativeY(DeviceNode node, string portName, PortDirection direction)
    {
        if (node.DeviceType == null) return 38;
        
        const double headerHeight = 28;
        const double portHeight = 20;
        const double portSpacing = 4;
        const double marginTop = 4;
        
        int portIndex;
        
        if (direction == PortDirection.Input)
        {
            portIndex = node.DeviceType.InputPorts.ToList().FindIndex(p => p.Name == portName);
            if (portIndex < 0) portIndex = 0;
        }
        else if (direction == PortDirection.Output)
        {
            portIndex = node.DeviceType.OutputPorts.ToList().FindIndex(p => p.Name == portName);
            if (portIndex < 0) portIndex = 0;
        }
        else
        {
            var outputCount = node.DeviceType.OutputPorts.Count;
            portIndex = outputCount + node.DeviceType.ErrorPorts.ToList().FindIndex(p => p.Name == portName);
            if (portIndex < outputCount) portIndex = outputCount;
        }
        
        return headerHeight + marginTop + portIndex * (portHeight + portSpacing) + portHeight / 2;
    }

    /// <summary>
    /// 根据屏幕坐标查找端口
    /// 使用适中的感应区域，通过高亮效果提高用户体验
    /// </summary>
    private DevicePort? FindPortAtPosition(DeviceNode node, Point screenPosition)
    {
        if (node.DeviceType == null) return null;
        if (!_nodeControls.TryGetValue(node.Id, out var control)) return null;
        
        // 将屏幕坐标转换为文档坐标，再转换为节点内坐标
        var canvasX = (screenPosition.X - _translateX) / Zoom;
        var canvasY = (screenPosition.Y - _translateY) / Zoom;
        var localX = canvasX - node.Position.X;
        var localY = canvasY - node.Position.Y;
        
        var allPorts = node.DeviceType.InputPorts
            .Concat(node.DeviceType.OutputPorts)
            .Concat(node.DeviceType.ErrorPorts)
            .ToList();
        
        // 保持原有感应半径
        var hitRadius = 15;
        
        DevicePort? closestPort = null;
        double closestDistance = double.MaxValue;
        
        foreach (var port in allPorts)
        {
            var portLocalY = GetPortRelativeY(node, port.Name, port.Direction);
            var portLocalX = port.Direction == PortDirection.Input ? 0 : NodeWidth;
            
            var distance = Math.Sqrt(
                Math.Pow(localX - portLocalX, 2) + 
                Math.Pow(localY - portLocalY, 2)
            );
            
            if (distance < hitRadius && distance < closestDistance)
            {
                closestDistance = distance;
                closestPort = port;
            }
        }
        
        return closestPort;
    }

    #endregion

    #region 连线创建方法

    private void TryCreateConnection(DeviceNode sourceNode, string sourcePort, DeviceNode targetNode, string targetPort)
    {
        var existingConnections = Connections.Select(c => new DeviceConnection
        {
            SourceNodeId = c.SourceNodeId,
            SourcePort = c.SourcePort,
            TargetNodeId = c.TargetNodeId,
            TargetPort = c.TargetPort
        }).ToList();
        
        var result = _validator.ValidateConnection(sourceNode, sourcePort, targetNode, targetPort, existingConnections);
        
        if (result.IsValid)
        {
            ConnectionCreated?.Invoke(this, new ConnectionCreatedEventArgs
            {
                SourceNode = sourceNode,
                SourcePort = sourcePort,
                TargetNode = targetNode,
                TargetPort = targetPort,
                Direction = _dragPortDirection
            });
        }
    }

    #endregion

    #region 连线管理方法

    public void AddConnection(ConnectionViewModel connection)
    {
        if (_connectionControls.ContainsKey(connection.Id)) return;
        
        var sourceNode = Nodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
        var targetNode = Nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
        
        if (sourceNode == null || targetNode == null) return;
        
        var control = new ConnectionControl
        {
            ConnectionId = connection.Id,
            SourceNodeId = connection.SourceNodeId,
            SourcePort = connection.SourcePort,
            TargetNodeId = connection.TargetNodeId,
            TargetPort = connection.TargetPort,
            ConnectionType = connection.Direction
        };
        
        // 初始位置先设置为节点中心
        control.StartPoint = new Point(
            sourceNode.Position.X * Zoom + _translateX + NodeWidth * Zoom / 2,
            sourceNode.Position.Y * Zoom + _translateY + NodeHeight * Zoom / 2
        );
        control.EndPoint = new Point(
            targetNode.Position.X * Zoom + _translateX + NodeWidth * Zoom / 2,
            targetNode.Position.Y * Zoom + _translateY + NodeHeight * Zoom / 2
        );
        
        control.SelectionChanged += (s, e) =>
        {
            if (control.IsSelected)
            {
                SelectedConnection = connection;
            }
            else
            {
                if (SelectedConnection?.Id == connection.Id)
                {
                    SelectedConnection = null;
                }
            }
        };
        
        control.EndpointDragStarted += OnEndpointDragStarted;
        control.EndpointDragging += OnEndpointDragging;
        control.EndpointDragCompleted += OnEndpointDragCompleted;
        
        _connectionControls[connection.Id] = control;
        
        // 连线添加到最后，显示在节点上方
        Children.Add(control);
        
        // 延迟更新连线位置，确保布局完成
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UpdateConnectionPositionActual(control, sourceNode, targetNode, connection.SourcePort, connection.TargetPort);
        });
        
        LogHelper.LogInfo("FlowEditor", "添加连线: {Source}.{SPort} -> {Target}.{TPort}",
            sourceNode.Title, connection.SourcePort, targetNode.Title, connection.TargetPort);
    }

    public void RemoveConnection(ConnectionViewModel connection)
    {
        if (_connectionControls.TryGetValue(connection.Id, out var control))
        {
            Children.Remove(control);
            _connectionControls.Remove(connection.Id);
        }
    }

    public void DeleteConnection(ConnectionViewModel connection)
    {
        RemoveConnection(connection);
        ConnectionDeleted?.Invoke(this, new ConnectionDeletedEventArgs { Connection = connection });
        SelectedConnection = null;
    }
    
    private PortControl? _hoveredPort;
    
    private void OnEndpointDragStarted(object? sender, EndpointDragEventArgs e)
    {
        if (sender is ConnectionControl control)
        {
            _draggingConnection = Connections.FirstOrDefault(c => c.Id == control.ConnectionId);
            _isDraggingStartEndpoint = e.IsStartEndpoint;
            _hoveredPort = null;
        }
    }
    
    private void OnEndpointDragging(object? sender, EndpointDragEventArgs e)
    {
        if (sender is ConnectionControl control && _draggingConnection != null)
        {
            if (_isDraggingStartEndpoint)
            {
                control.StartPoint = e.Position;
            }
            else
            {
                control.EndPoint = e.Position;
            }
            control.InvalidateVisual();
            
            // 检测悬停的端口并高亮
            var targetNode = GetNodeAtPosition(e.Position);
            if (targetNode != null)
            {
                var targetPort = FindPortAtPosition(targetNode, e.Position);
                if (targetPort != null && _nodeControls.TryGetValue(targetNode.Id, out var nodeControl))
                {
                    var portControl = nodeControl.GetPortControl(targetPort.Name);
                    if (portControl != null && portControl != _hoveredPort)
                    {
                        // 取消之前的高亮
                        if (_hoveredPort != null)
                        {
                            _hoveredPort.IsHoveredByDrag = false;
                        }
                        // 设置新高亮
                        _hoveredPort = portControl;
                        _hoveredPort.IsHoveredByDrag = true;
                    }
                }
                else if (_hoveredPort != null)
                {
                    _hoveredPort.IsHoveredByDrag = false;
                    _hoveredPort = null;
                }
            }
            else if (_hoveredPort != null)
            {
                _hoveredPort.IsHoveredByDrag = false;
                _hoveredPort = null;
            }
        }
    }
    
    private void OnEndpointDragCompleted(object? sender, EndpointDragEventArgs e)
    {
        // 清除端口高亮
        if (_hoveredPort != null)
        {
            _hoveredPort.IsHoveredByDrag = false;
            _hoveredPort = null;
        }
        
        if (sender is ConnectionControl control && _draggingConnection != null)
        {
            var targetNode = GetNodeAtPosition(e.Position);
            var targetPort = targetNode != null ? FindPortAtPosition(targetNode, e.Position) : null;
            
            bool connectionUpdated = false;
            
            if (targetPort != null)
            {
                // 验证端口方向规则
                if (_isDraggingStartEndpoint)
                {
                    // 起点：只能是输出端口或错误端口（不能是输入端口）
                    if (targetPort.Direction != PortDirection.Input)
                    {
                        // 更新起点
                        _draggingConnection.SourceNodeId = targetNode!.Id;
                        _draggingConnection.SourcePort = targetPort.Name;
                        _draggingConnection.Direction = targetPort.Direction;
                        
                        // 更新连线颜色
                        control.ConnectionType = targetPort.Direction;
                        connectionUpdated = true;
                        
                        LogHelper.LogInfo("FlowEditor", "连线起点已更新: {Port}", targetPort.Name);
                    }
                    else
                    {
                        LogHelper.LogWarning("FlowEditor", "输入端口不能作为连线起点");
                    }
                }
                else
                {
                    // 终点：只能是输入端口（不能是输出端口或错误端口）
                    if (targetPort.Direction == PortDirection.Input)
                    {
                        // 更新终点
                        _draggingConnection.TargetNodeId = targetNode!.Id;
                        _draggingConnection.TargetPort = targetPort.Name;
                        connectionUpdated = true;
                        
                        LogHelper.LogInfo("FlowEditor", "连线终点已更新: {Port}", targetPort.Name);
                    }
                    else
                    {
                        LogHelper.LogWarning("FlowEditor", "输出/错误端口不能作为连线终点");
                    }
                }
            }
            
            if (connectionUpdated)
            {
                // 更新连线位置
                UpdateConnectionPositionActual(control, 
                    Nodes.First(n => n.Id == _draggingConnection.SourceNodeId),
                    Nodes.First(n => n.Id == _draggingConnection.TargetNodeId),
                    _draggingConnection.SourcePort, _draggingConnection.TargetPort);
            }
            else
            {
                // 拖到空白区域或无效端口，恢复原位
                var sourceNode = Nodes.FirstOrDefault(n => n.Id == _draggingConnection.SourceNodeId);
                var targetNode1 = Nodes.FirstOrDefault(n => n.Id == _draggingConnection.TargetNodeId);
                if (sourceNode != null && targetNode1 != null)
                {
                    UpdateConnectionPositionActual(control, sourceNode, targetNode1, 
                        _draggingConnection.SourcePort, _draggingConnection.TargetPort);
                }
            }
            
            _draggingConnection = null;
        }
    }

    private void UpdateConnectionPositionActual(ConnectionControl control, DeviceNode sourceNode, DeviceNode targetNode, string sourcePort, string targetPort)
    {
        var sourcePos = GetPortPositionActual(sourceNode, sourcePort, PortDirection.Output);
        var targetPos = GetPortPositionActual(targetNode, targetPort, PortDirection.Input);
        
        LogHelper.LogInfo("FlowEditor", "更新连线: {Source}.{SPort}({SX:F1},{SY:F1}) -> {Target}.{TPort}({TX:F1},{TY:F1})",
            sourceNode.Title, sourcePort, sourcePos.X, sourcePos.Y,
            targetNode.Title, targetPort, targetPos.X, targetPos.Y);
        
        control.StartPoint = sourcePos;
        control.EndPoint = targetPos;
        control.InvalidateVisual();
    }

    private void UpdateConnectionsForNode(string nodeId)
    {
        foreach (var conn in Connections.Where(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId))
        {
            if (_connectionControls.TryGetValue(conn.Id, out var control))
            {
                var sourceNode = Nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId);
                var targetNode = Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
                
                if (sourceNode != null && targetNode != null)
                {
                    UpdateConnectionPositionActual(control, sourceNode, targetNode, conn.SourcePort, conn.TargetPort);
                }
            }
        }
    }

    private void UpdateAllConnections()
    {
        ScheduleConnectionUpdate();
    }
    
    private void UpdateAllConnectionsInternal()
    {
        foreach (var conn in Connections)
        {
            if (_connectionControls.TryGetValue(conn.Id, out var control))
            {
                var sourceNode = Nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId);
                var targetNode = Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
                
                if (sourceNode != null && targetNode != null)
                {
                    UpdateConnectionPositionActual(control, sourceNode, targetNode, conn.SourcePort, conn.TargetPort);
                }
            }
        }
    }

    #endregion

    #region 集合变化处理

    private void OnNodesChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is ObservableCollection<DeviceNode> oldNodes)
        {
            oldNodes.CollectionChanged -= OnNodesCollectionChanged;
        }

        if (e.NewValue is ObservableCollection<DeviceNode> newNodes)
        {
            newNodes.CollectionChanged += OnNodesCollectionChanged;
            
            foreach (var node in newNodes)
            {
                if (!_nodeControls.ContainsKey(node.Id))
                {
                    AddNode(node);
                }
            }
        }
    }

    private void OnNodesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (DeviceNode node in e.NewItems)
            {
                AddNode(node);
            }
        }

        if (e.OldItems != null)
        {
            foreach (DeviceNode node in e.OldItems)
            {
                RemoveNode(node);
            }
        }
    }

    private void OnConnectionsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is ObservableCollection<ConnectionViewModel> oldConnections)
        {
            oldConnections.CollectionChanged -= OnConnectionsCollectionChanged;
        }

        if (e.NewValue is ObservableCollection<ConnectionViewModel> newConnections)
        {
            newConnections.CollectionChanged += OnConnectionsCollectionChanged;
            
            foreach (var conn in newConnections)
            {
                if (!_connectionControls.ContainsKey(conn.Id))
                {
                    AddConnection(conn);
                }
            }
        }
    }

    private void OnConnectionsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (ConnectionViewModel conn in e.NewItems)
            {
                AddConnection(conn);
            }
        }

        if (e.OldItems != null)
        {
            foreach (ConnectionViewModel conn in e.OldItems)
            {
                RemoveConnection(conn);
            }
        }
    }

    #endregion

    #region 公共刷新方法

    public void RefreshNodes()
    {
        _nodeControls.Clear();
        _connectionControls.Clear();
        Children.Clear();
        _gridLines.Clear();
        
        // 1. 先添加网格线（最底层）
        UpdateGrid();
        
        // 2. 再添加节点（在网格线上层）
        if (Nodes != null)
        {
            foreach (var node in Nodes)
            {
                AddNode(node);
            }
        }
        
        // 3. 最后添加连线（在节点上层）
        if (Connections != null)
        {
            foreach (var conn in Connections)
            {
                AddConnection(conn);
            }
        }
        
        ScheduleConnectionUpdate();
    }
    
    public void RefreshConnections()
    {
        UpdateAllNodePositions();
        ScheduleConnectionUpdate();
    }

    #endregion
}

#region 事件参数类

public class ConnectionCreatedEventArgs : EventArgs
{
    public DeviceNode SourceNode { get; set; } = null!;
    public string SourcePort { get; set; } = string.Empty;
    public DeviceNode TargetNode { get; set; } = null!;
    public string TargetPort { get; set; } = string.Empty;
    public PortDirection Direction { get; set; } = PortDirection.Output;
}

public class ConnectionDeletedEventArgs : EventArgs
{
    public ConnectionViewModel Connection { get; set; } = null!;
}

#endregion
