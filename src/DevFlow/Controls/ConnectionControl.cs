using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using DevFlow.Models;
using DevFlow.Services;

namespace DevFlow.Controls;

/// <summary>
/// 连线控件
/// 用于绘制节点之间的贝塞尔曲线连接线
/// 支持选中状态、悬停效果、端点拖拽
/// </summary>
public class ConnectionControl : Control
{
    #region 依赖属性定义

    /// <summary>
    /// 连线起点（屏幕坐标，相对于 FlowEditor）
    /// </summary>
    public static readonly StyledProperty<Point> StartPointProperty =
        AvaloniaProperty.Register<ConnectionControl, Point>(nameof(StartPoint));

    /// <summary>
    /// 连线终点（屏幕坐标，相对于 FlowEditor）
    /// </summary>
    public static readonly StyledProperty<Point> EndPointProperty =
        AvaloniaProperty.Register<ConnectionControl, Point>(nameof(EndPoint));

    /// <summary>
    /// 连线类型（决定颜色）
    /// </summary>
    public static readonly StyledProperty<PortDirection> ConnectionTypeProperty =
        AvaloniaProperty.Register<ConnectionControl, PortDirection>(nameof(ConnectionType), PortDirection.Output);

    /// <summary>
    /// 是否选中
    /// </summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ConnectionControl, bool>(nameof(IsSelected));

    /// <summary>
    /// 是否正在执行中
    /// </summary>
    public static readonly StyledProperty<bool> IsExecutingProperty =
        AvaloniaProperty.Register<ConnectionControl, bool>(nameof(IsExecuting));

    #endregion

    #region 属性访问器

    /// <summary>连线起点</summary>
    public Point StartPoint
    {
        get => GetValue(StartPointProperty);
        set => SetValue(StartPointProperty, value);
    }

    /// <summary>连线终点</summary>
    public Point EndPoint
    {
        get => GetValue(EndPointProperty);
        set => SetValue(EndPointProperty, value);
    }

    /// <summary>连线类型</summary>
    public PortDirection ConnectionType
    {
        get => GetValue(ConnectionTypeProperty);
        set => SetValue(ConnectionTypeProperty, value);
    }

    /// <summary>是否选中</summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>是否正在执行中</summary>
    public bool IsExecuting
    {
        get => GetValue(IsExecutingProperty);
        set => SetValue(IsExecutingProperty, value);
    }

    #endregion

    #region 连线元数据属性

    /// <summary>连线ID</summary>
    public string ConnectionId { get; set; } = string.Empty;
    
    /// <summary>源节点ID</summary>
    public string SourceNodeId { get; set; } = string.Empty;
    
    /// <summary>源端口名称</summary>
    public string SourcePort { get; set; } = string.Empty;
    
    /// <summary>目标节点ID</summary>
    public string TargetNodeId { get; set; } = string.Empty;
    
    /// <summary>目标端口名称</summary>
    public string TargetPort { get; set; } = string.Empty;

    #endregion

    #region 私有字段

    /// <summary>是否悬停状态</summary>
    private bool _isHovered;
    
    /// <summary>是否正在拖拽起点端点</summary>
    private bool _isDraggingStart;
    
    /// <summary>是否正在拖拽终点端点</summary>
    private bool _isDraggingEnd;
    
    /// <summary>拖拽开始时的位置</summary>
    private Point _dragStartPos;

    #endregion

    #region 事件定义

    /// <summary>端点拖拽开始事件</summary>
    public event EventHandler<EndpointDragEventArgs>? EndpointDragStarted;
    
    /// <summary>端点拖拽中事件</summary>
    public event EventHandler<EndpointDragEventArgs>? EndpointDragging;
    
    /// <summary>端点拖拽完成事件</summary>
    public event EventHandler<EndpointDragEventArgs>? EndpointDragCompleted;
    
    /// <summary>选中状态变化事件</summary>
    public event EventHandler? SelectionChanged;

    #endregion

    #region 构造函数

    /// <summary>
    /// 构造函数
    /// 初始化控件并注册事件处理器
    /// </summary>
    public ConnectionControl()
    {
        IsHitTestVisible = true;
        Cursor = Cursor.Parse("Hand");
        
        // 注册鼠标事件
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    #endregion

    #region 颜色计算

    /// <summary>
    /// 根据连线类型获取基础颜色
    /// </summary>
    /// <returns>连线颜色</returns>
    private Color GetBaseColor()
    {
        return ConnectionType switch
        {
            PortDirection.Input => Color.Parse("#4CAF50"),   // 绿色 - 输入
            PortDirection.Output => Color.Parse("#2196F3"),  // 蓝色 - 输出
            PortDirection.Error => Color.Parse("#F44336"),   // 红色 - 错误
            _ => Color.Parse("#2196F3")
        };
    }

    /// <summary>
    /// 将颜色变亮
    /// </summary>
    /// <param name="color">原始颜色</param>
    /// <returns>变亮后的颜色</returns>
    private static Color LightenColor(Color color)
    {
        return Color.FromRgb(
            (byte)Math.Min(color.R + 40, 255),
            (byte)Math.Min(color.G + 40, 255),
            (byte)Math.Min(color.B + 40, 255)
        );
    }

    #endregion

    #region 属性变化处理

    /// <summary>
    /// 处理属性变化事件
    /// 当相关属性变化时触发重绘
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // 监听需要触发重绘的属性
        if (change.Property == StartPointProperty || 
            change.Property == EndPointProperty ||
            change.Property == ConnectionTypeProperty ||
            change.Property == IsSelectedProperty ||
            change.Property == IsExecutingProperty)
        {
            InvalidateVisual();
        }
    }

    #endregion

    #region 渲染方法

    /// <summary>
    /// 渲染连线
    /// 绘制贝塞尔曲线、箭头和端点
    /// </summary>
    /// <param name="context">绘图上下文</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // 如果起点或终点无效则不渲染
        if (StartPoint == default || EndPoint == default) return;

        // 计算颜色和线宽
        var baseColor = GetBaseColor();
        
        // 拖拽端点时使用基础颜色，不使用选中颜色
        bool isDragging = _isDraggingStart || _isDraggingEnd;
        
        // 只有在选中且不在拖拽时才显示白色
        var strokeColor = (IsSelected && !isDragging) ? Colors.White : (_isHovered ? LightenColor(baseColor) : baseColor);
        var strokeThickness = IsSelected || _isHovered ? 3 : 2;

        // 绘制贝塞尔曲线
        var geometry = CreateBezierGeometry();
        if (geometry == null) return;

        var pen = new Pen(new SolidColorBrush(strokeColor), strokeThickness);
        context.DrawGeometry(null, pen, geometry);

        // 绘制箭头
        var arrowGeometry = CreateArrowGeometry();
        if (arrowGeometry != null)
        {
            context.DrawGeometry(new SolidColorBrush(strokeColor), null, arrowGeometry);
        }

        // 选中、悬停或拖拽时绘制端点
        if (IsSelected || _isHovered || isDragging)
        {
            DrawEndpoint(context, StartPoint, baseColor, _isDraggingStart);
            DrawEndpoint(context, EndPoint, baseColor, _isDraggingEnd);
        }
    }

    /// <summary>
    /// 绘制端点圆形
    /// </summary>
    /// <param name="context">绘图上下文</param>
    /// <param name="position">端点位置</param>
    /// <param name="color">端点颜色</param>
    /// <param name="isDragging">是否正在拖拽</param>
    private void DrawEndpoint(DrawingContext context, Point position, Color color, bool isDragging)
    {
        var radius = isDragging ? 8 : 6;
        var brush = new SolidColorBrush(isDragging ? Colors.White : color);
        var pen = new Pen(new SolidColorBrush(Colors.White), 2);
        
        context.DrawEllipse(brush, pen, position, radius, radius);
    }

    #endregion

    #region 贝塞尔曲线生成

    /// <summary>
    /// 创建三次贝塞尔曲线路径
    /// 控制点根据起点和终点的距离自动调整
    /// </summary>
    /// <returns>贝塞尔曲线路径几何</returns>
    private PathGeometry? CreateBezierGeometry()
    {
        if (StartPoint == default || EndPoint == default) return null;

        // 计算控制点偏移量，确保曲线平滑
        var controlOffset = Math.Max(50, Math.Abs(EndPoint.X - StartPoint.X) * 0.5);

        // 三次贝塞尔曲线的四个控制点
        var p0 = StartPoint;                                                    // 起点
        var p3 = EndPoint;                                                      // 终点
        var p1 = new Point(p0.X + controlOffset, p0.Y);                        // 控制点1
        var p2 = new Point(p3.X - controlOffset, p3.Y);                        // 控制点2

        // 创建路径几何
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = p0,
            IsClosed = false
        };

        // 添加三次贝塞尔曲线段
        var bezierSegment = new BezierSegment
        {
            Point1 = p1,
            Point2 = p2,
            Point3 = p3
        };

        figure.Segments.Add(bezierSegment);
        geometry.Figures.Add(figure);

        return geometry;
    }

    /// <summary>
    /// 创建箭头路径
    /// 箭头方向与曲线末端切线方向一致
    /// </summary>
    /// <returns>箭头路径几何</returns>
    private PathGeometry? CreateArrowGeometry()
    {
        if (StartPoint == default || EndPoint == default) return null;

        var p3 = EndPoint;
        
        // 计算贝塞尔曲线控制点
        var controlOffset = Math.Max(50, Math.Abs(EndPoint.X - StartPoint.X) * 0.5);
        var p2 = new Point(p3.X - controlOffset, p3.Y);
        var p1 = new Point(StartPoint.X + controlOffset, StartPoint.Y);
        var p0 = StartPoint;

        // 计算曲线末端的点和其前一个点，用于确定切线方向
        var endPoint = GetBezierPoint(p0, p1, p2, p3, 1.0);
        var prevPoint = GetBezierPoint(p0, p1, p2, p3, 0.98);
        
        // 计算切线方向向量
        var direction = new Vector(endPoint.X - prevPoint.X, endPoint.Y - prevPoint.Y);
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (length < 0.001) return null;
        
        // 归一化方向向量
        direction = new Vector(direction.X / length, direction.Y / length);
        
        // 箭头尺寸
        var arrowSize = 10;
        
        // 计算垂直向量
        var perpX = -direction.Y;
        var perpY = direction.X;
        
        // 箭头顶点
        var tipX = endPoint.X;
        var tipY = endPoint.Y;
        
        // 箭头底部中心点
        var baseX = tipX - direction.X * arrowSize;
        var baseY = tipY - direction.Y * arrowSize;
        
        // 箭头两个底角点
        var leftX = baseX + perpX * (arrowSize * 0.5);
        var leftY = baseY + perpY * (arrowSize * 0.5);
        
        var rightX = baseX - perpX * (arrowSize * 0.5);
        var rightY = baseY - perpY * (arrowSize * 0.5);

        // 创建三角形箭头路径
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(tipX, tipY),
            IsClosed = true,
            IsFilled = true
        };

        figure.Segments.Add(new LineSegment { Point = new Point(leftX, leftY) });
        figure.Segments.Add(new LineSegment { Point = new Point(rightX, rightY) });

        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>
    /// 计算三次贝塞尔曲线上指定参数位置的点
    /// </summary>
    /// <param name="p0">起点</param>
    /// <param name="p1">控制点1</param>
    /// <param name="p2">控制点2</param>
    /// <param name="p3">终点</param>
    /// <param name="t">参数 [0, 1]</param>
    /// <returns>曲线上的点</returns>
    private static Point GetBezierPoint(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1 - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;

        var x = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
        var y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;

        return new Point(x, y);
    }

    #endregion

    #region 鼠标事件处理

    /// <summary>
    /// 处理鼠标进入事件
    /// 设置悬停状态
    /// </summary>
    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _isHovered = true;
        InvalidateVisual();
    }

    /// <summary>
    /// 处理鼠标离开事件
    /// 取消悬停状态
    /// </summary>
    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isHovered = false;
        InvalidateVisual();
    }

    /// <summary>
    /// 处理鼠标按下事件
    /// 检测是否点击端点或连线
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 修复：使用相对于 FlowEditor 的坐标进行端点检测
        var flowEditor = this.FindAncestorOfType<FlowEditor>();
        if (flowEditor == null) return;
        
        var position = e.GetPosition(flowEditor);
        
        // 计算到起点和终点的距离
        var distToStart = Math.Sqrt(Math.Pow(position.X - StartPoint.X, 2) + Math.Pow(position.Y - StartPoint.Y, 2));
        var distToEnd = Math.Sqrt(Math.Pow(position.X - EndPoint.X, 2) + Math.Pow(position.Y - EndPoint.Y, 2));

        // 检测是否点击了起点端点
        if (distToStart < 15)
        {
            _isDraggingStart = true;
            _dragStartPos = position;
            e.Handled = true;
            e.Pointer.Capture(this);
            
            EndpointDragStarted?.Invoke(this, new EndpointDragEventArgs 
            { 
                IsStartEndpoint = true, 
                Position = StartPoint 
            });
            
            LogHelper.LogDebug("ConnectionControl", "开始拖拽起点端点");
        }
        // 检测是否点击了终点端点
        else if (distToEnd < 15)
        {
            _isDraggingEnd = true;
            _dragStartPos = position;
            e.Handled = true;
            e.Pointer.Capture(this);
            
            EndpointDragStarted?.Invoke(this, new EndpointDragEventArgs 
            { 
                IsStartEndpoint = false, 
                Position = EndPoint 
            });
            
            LogHelper.LogDebug("ConnectionControl", "开始拖拽终点端点");
        }
        // 点击连线本身，切换选中状态
        else
        {
            IsSelected = !IsSelected;
            InvalidateVisual();
            e.Handled = true;
            
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            
            LogHelper.LogDebug("ConnectionControl", "连线选中: {IsSelected}", IsSelected);
        }
    }

    /// <summary>
    /// 处理鼠标移动事件
    /// 拖拽端点时更新位置
    /// </summary>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingStart && !_isDraggingEnd) return;

        // 获取相对于 FlowEditor 的位置
        var flowEditor = this.FindAncestorOfType<FlowEditor>();
        if (flowEditor == null) return;
        
        var position = e.GetPosition(flowEditor);
        
        // 更新端点位置
        if (_isDraggingStart)
        {
            StartPoint = position;
        }
        else
        {
            EndPoint = position;
        }
        
        InvalidateVisual();
        
        // 触发拖拽中事件
        EndpointDragging?.Invoke(this, new EndpointDragEventArgs
        {
            IsStartEndpoint = _isDraggingStart,
            Position = position
        });
        
        e.Handled = true;
    }

    /// <summary>
    /// 处理鼠标释放事件
    /// 完成端点拖拽
    /// </summary>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingStart && !_isDraggingEnd) return;

        // 获取相对于 FlowEditor 的位置
        var flowEditor = this.FindAncestorOfType<FlowEditor>();
        if (flowEditor != null)
        {
            var position = e.GetPosition(flowEditor);
            
            // 触发拖拽完成事件
            EndpointDragCompleted?.Invoke(this, new EndpointDragEventArgs
            {
                IsStartEndpoint = _isDraggingStart,
                Position = position
            });
        }
        
        // 重置拖拽状态
        _isDraggingStart = false;
        _isDraggingEnd = false;
        InvalidateVisual();
        e.Handled = true;
    }

    #endregion

    #region 布局测量

    /// <summary>
    /// 测量控件所需大小
    /// </summary>
    /// <param name="availableSize">可用大小</param>
    /// <returns>所需大小</returns>
    protected override Size MeasureCore(Size availableSize)
    {
        // 返回包含起点和终点的边界框
        return new Size(
            Math.Abs(EndPoint.X - StartPoint.X) + 100,
            Math.Abs(EndPoint.Y - StartPoint.Y) + 100
        );
    }

    #endregion
}

#region 事件参数类

/// <summary>
/// 端点拖拽事件参数
/// </summary>
public class EndpointDragEventArgs : EventArgs
{
    /// <summary>是否是起点端点</summary>
    public bool IsStartEndpoint { get; set; }
    
    /// <summary>端点当前位置（相对于 FlowEditor）</summary>
    public Point Position { get; set; }
}

#endregion
