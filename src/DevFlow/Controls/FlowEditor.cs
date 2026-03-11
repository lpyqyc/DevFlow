using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using DevFlow.Models;
using DevFlow.ViewModels;

namespace DevFlow.Controls;

public class FlowEditor : Canvas
{
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

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set
        {
            SetValue(ZoomProperty, value);
            UpdateAllNodePositions();
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

    private double _translateX;
    private double _translateY;
    private Point _lastMousePosition;
    private bool _isPanning;
    private DeviceNode? _draggingNode;
    private Point _dragStartPosition;
    private Point _nodeStartPosition;
    private readonly Dictionary<string, NodeControl> _nodeControls = new();
    private readonly List<Line> _gridLines = new();

    public FlowEditor()
    {
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        ClipToBounds = true;

        AddHandler(PointerPressedEvent, OnPointerPressed);
        AddHandler(PointerMovedEvent, OnPointerMoved);
        AddHandler(PointerReleasedEvent, OnPointerReleased);
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged);
        
        PropertyChanged += (s, e) =>
        {
            if (e.Property == BoundsProperty)
            {
                UpdateGrid();
            }
        };
    }

    static FlowEditor()
    {
        NodesProperty.Changed.AddClassHandler<FlowEditor>((editor, e) => editor.OnNodesChanged(e));
        ConnectionsProperty.Changed.AddClassHandler<FlowEditor>((editor, e) => editor.InvalidateVisual());
    }

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
                StrokeThickness = 1
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
                StrokeThickness = 1
            };
            _gridLines.Add(line);
            Children.Insert(0, line);
        }
    }

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
            _draggingNode = clickedNode;
            _nodeStartPosition = clickedNode.Position;
            _dragStartPosition = new Point(
                (position.X - _translateX) / Zoom,
                (position.Y - _translateY) / Zoom
            );
        }
        else
        {
            SelectedNode = null;
        }
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
        }

        _lastMousePosition = position;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPanning = false;
        _draggingNode = null;
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var position = e.GetPosition(this);
        var zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
        var newZoom = Zoom * zoomFactor;

        newZoom = Math.Max(0.25, Math.Min(4.0, newZoom));

        var oldZoom = Zoom;
        Zoom = newZoom;

        var mouseX = (position.X - _translateX) / oldZoom;
        var mouseY = (position.Y - _translateY) / oldZoom;

        _translateX = position.X - mouseX * newZoom;
        _translateY = position.Y - mouseY * newZoom;

        UpdateAllNodePositions();
        UpdateGrid();
    }

    private DeviceNode? GetNodeAtPosition(Point position)
    {
        var canvasPos = new Point(
            (position.X - _translateX) / Zoom,
            (position.Y - _translateY) / Zoom
        );

        foreach (var node in Nodes.Reverse())
        {
            if (canvasPos.X >= node.Position.X && canvasPos.X <= node.Position.X + 180 &&
                canvasPos.Y >= node.Position.Y && canvasPos.Y <= node.Position.Y + 100)
            {
                return node;
            }
        }

        return null;
    }

    public void AddNode(DeviceNode node)
    {
        if (_nodeControls.ContainsKey(node.Id)) return;
        
        var control = new NodeControl { DataContext = node };
        control.Tag = node;
        
        SetLeft(control, node.Position.X * Zoom + _translateX);
        SetTop(control, node.Position.Y * Zoom + _translateY);
        
        _nodeControls[node.Id] = control;
        Children.Add(control);
    }

    public void RemoveNode(DeviceNode node)
    {
        if (_nodeControls.TryGetValue(node.Id, out var control))
        {
            Children.Remove(control);
            _nodeControls.Remove(node.Id);
        }
    }

    private void UpdateNodeControlPosition(DeviceNode node)
    {
        if (_nodeControls.TryGetValue(node.Id, out var control))
        {
            SetLeft(control, node.Position.X * Zoom + _translateX);
            SetTop(control, node.Position.Y * Zoom + _translateY);
            control.Width = 180 * Zoom;
            control.Height = 100 * Zoom;
        }
    }

    private void UpdateAllNodePositions()
    {
        foreach (var node in Nodes)
        {
            UpdateNodeControlPosition(node);
        }
    }

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

    public void RefreshNodes()
    {
        _nodeControls.Clear();
        Children.Clear();
        _gridLines.Clear();
        
        if (Nodes != null)
        {
            foreach (var node in Nodes)
            {
                AddNode(node);
            }
        }
        UpdateGrid();
    }
}
