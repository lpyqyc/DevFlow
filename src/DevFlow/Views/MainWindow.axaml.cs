using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DevFlow.Controls;
using DevFlow.Models;
using DevFlow.Services;
using DevFlow.ViewModels;

namespace DevFlow.Views;

public partial class MainWindow : Window
{
    private FlowEditor? _flowEditor;
    private const string DeviceTypeFormat = "DeviceType";

    public MainWindow()
    {
        InitializeComponent();
        LogHelper.LogInfo("MainWindow", "主窗口初始化完成");
        
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        _flowEditor = this.FindControl<FlowEditor>("FlowEditorControl");
        if (_flowEditor != null)
        {
            _flowEditor.ConnectionCreated += OnConnectionCreated;
            _flowEditor.ConnectionDeleted += OnConnectionDeleted;
            LogHelper.LogInfo("MainWindow", "已订阅 FlowEditor 连接事件");
        }
        
        if (DataContext is MainWindowViewModel vm)
        {
            vm.EditorViewModel.ConnectionsRefreshRequested += OnConnectionsRefreshRequested;
            vm.EditorViewModel.ViewportChanged += OnViewportChanged;
            vm.EditorViewModel.DocumentReset += OnDocumentReset;
            vm.EditorViewModel.FitToViewRequested += OnFitToViewRequested;
        }
    }
    
    private void OnViewportChanged(object? sender, ViewportState e)
    {
        if (_flowEditor != null)
        {
            _flowEditor.SetViewport(e.Zoom, e.TranslateX, e.TranslateY);
            LogHelper.LogInfo("MainWindow", "视图状态已恢复: Zoom={Zoom}", e.Zoom);
        }
    }
    
    private void OnFitToViewRequested(object? sender, EventArgs e)
    {
        if (_flowEditor != null)
        {
            _flowEditor.FitToView();
            if (DataContext is MainWindowViewModel vm)
            {
                vm.EditorViewModel.Zoom = _flowEditor.Zoom;
                vm.EditorViewModel.TranslateX = _flowEditor.TranslateX;
                vm.EditorViewModel.TranslateY = _flowEditor.TranslateY;
            }
            LogHelper.LogInfo("MainWindow", "自动布局后适应视窗");
        }
    }
    
    private void OnDocumentReset(object? sender, EventArgs e)
    {
        if (_flowEditor != null)
        {
            _flowEditor.RefreshNodes();
            LogHelper.LogInfo("MainWindow", "文档已重置，刷新节点");
        }
    }
    
    private void OnConnectionsRefreshRequested(object? sender, EventArgs e)
    {
        if (_flowEditor != null)
        {
            _flowEditor.RefreshConnections();
            LogHelper.LogInfo("MainWindow", "自动布局后刷新连线");
        }
    }
    
    private void OnZoomInClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_flowEditor != null)
        {
            _flowEditor.ZoomInCentered();
            if (DataContext is MainWindowViewModel vm)
            {
                vm.EditorViewModel.Zoom = _flowEditor.Zoom;
            }
        }
    }
    
    private void OnZoomOutClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_flowEditor != null)
        {
            _flowEditor.ZoomOutCentered();
            if (DataContext is MainWindowViewModel vm)
            {
                vm.EditorViewModel.Zoom = _flowEditor.Zoom;
            }
        }
    }
    
    private void OnResetZoomClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_flowEditor != null)
        {
            _flowEditor.ResetZoomCentered();
            if (DataContext is MainWindowViewModel vm)
            {
                vm.EditorViewModel.Zoom = _flowEditor.Zoom;
                vm.EditorViewModel.TranslateX = _flowEditor.TranslateX;
                vm.EditorViewModel.TranslateY = _flowEditor.TranslateY;
            }
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        LogHelper.LogDebug("MainWindow", "OnKeyDown: Key={Key}", e.Key);
        
        if (_flowEditor != null)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                LogHelper.LogInfo("MainWindow", "Delete键按下, SelectedConnection={Selected}, SelectedNode={Node}", 
                    _flowEditor.SelectedConnection?.Id ?? "null",
                    _flowEditor.SelectedNode?.Title ?? "null");
                
                // 优先删除选中的连线
                if (_flowEditor.SelectedConnection != null)
                {
                    LogHelper.LogInfo("MainWindow", "删除选中连线: {ConnectionId}", _flowEditor.SelectedConnection.Id);
                    _flowEditor.DeleteConnection(_flowEditor.SelectedConnection);
                    e.Handled = true;
                }
                // 如果没有选中的连线，删除选中的节点
                else if (_flowEditor.SelectedNode != null)
                {
                    if (DataContext is MainWindowViewModel vm)
                    {
                        LogHelper.LogInfo("MainWindow", "删除选中节点: {Title}", _flowEditor.SelectedNode.Title);
                        vm.EditorViewModel.DeleteSelectedNodeCommand.Execute(null);
                        e.Handled = true;
                    }
                }
            }
            
            if (e.Key == Key.Escape)
            {
                _flowEditor.SelectedConnection = null;
                _flowEditor.SelectedNode = null;
                e.Handled = true;
            }
        }
    }

    private void OnConnectionCreated(object? sender, ConnectionCreatedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.EditorViewModel.CreateConnection(
                e.SourceNode, e.SourcePort,
                e.TargetNode, e.TargetPort,
                e.Direction);
            
            LogHelper.LogInfo("MainWindow", 
                "连接创建事件处理完成: {SourceNode}.{SourcePort} -> {TargetNode}.{TargetPort}, Direction={Direction}",
                e.SourceNode.Title, e.SourcePort,
                e.TargetNode.Title, e.TargetPort, e.Direction);
        }
    }

    private void OnConnectionDeleted(object? sender, ConnectionDeletedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.EditorViewModel.RemoveConnection(e.Connection);
            
            LogHelper.LogInfo("MainWindow", "连接删除事件处理完成: {ConnectionId}", e.Connection.Id);
        }
    }

    private void OnDeviceItemPressed(object? sender, PointerPressedEventArgs e)
    {
        LogHelper.LogInfo("MainWindow", "OnDeviceItemPressed 触发");
        
        if (sender is Border border && border.DataContext is DeviceTypeItem deviceType)
        {
            LogHelper.LogInfo("MainWindow", "开始拖拽设备: Name={Name}", deviceType.Name);
            
#pragma warning disable CS0618
            var data = new DataObject();
            data.Set(DeviceTypeFormat, deviceType);
            DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
#pragma warning restore CS0618
            
            LogHelper.LogInfo("MainWindow", "DoDragDrop 已调用");
        }
        else
        {
            LogHelper.LogWarning("MainWindow", "sender 不是 Border 或 DataContext 不是 DeviceTypeItem");
        }
    }

    private void OnDeviceItemDoubleTapped(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        LogHelper.LogInfo("MainWindow", "OnDeviceItemDoubleTapped 触发");
        
        if (sender is Border border && border.DataContext is DeviceTypeItem deviceType && DataContext is MainWindowViewModel vm)
        {
            var flowEditor = this.FindControl<FlowEditor>("FlowEditorControl");
            if (flowEditor != null)
            {
                var nodeX = 200 + vm.EditorViewModel.Nodes.Count * 50;
                var nodeY = 200 + vm.EditorViewModel.Nodes.Count * 50;
                
                LogHelper.LogInfo("MainWindow", "双击添加节点: DeviceType={DeviceType}, Position=({X},{Y})", 
                    deviceType.Name, nodeX, nodeY);
                
                var node = new Models.DeviceNode
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
                
                vm.EditorViewModel.Nodes.Add(node);
                
                LogHelper.LogInfo("MainWindow", "节点创建成功: Id={NodeId}, Title={Title}", node.Id, node.Title);
            }
        }
    }

    private void OnCanvasDragOver(object? sender, DragEventArgs e)
    {
        LogHelper.LogDebug("MainWindow", "DragOver 事件触发");
        
#pragma warning disable CS0618
        if (e.Data.Contains(DeviceTypeFormat))
#pragma warning restore CS0618
        {
            e.DragEffects = DragDropEffects.Copy;
            LogHelper.LogDebug("MainWindow", "DragOver: 设置 Copy 效果");
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
            LogHelper.LogDebug("MainWindow", "DragOver: 数据不包含 DeviceType");
        }
    }

    private void OnCanvasDrop(object? sender, DragEventArgs e)
    {
        LogHelper.LogInfo("MainWindow", "Drop事件触发");
        
#pragma warning disable CS0618
        var deviceTypeObj = e.Data.Get(DeviceTypeFormat);
#pragma warning restore CS0618
        
        LogHelper.LogInfo("MainWindow", "获取到数据: {Type}", deviceTypeObj?.GetType().Name ?? "null");
        
        if (deviceTypeObj is DeviceTypeItem deviceType && DataContext is MainWindowViewModel vm)
        {
            var flowEditor = this.FindControl<FlowEditor>("FlowEditorControl");
            if (flowEditor != null)
            {
                var screenPos = e.GetPosition(flowEditor);
                
                var canvasX = (screenPos.X - flowEditor.TranslateX) / flowEditor.Zoom;
                var canvasY = (screenPos.Y - flowEditor.TranslateY) / flowEditor.Zoom;
                
                var nodeX = canvasX - 90;
                var nodeY = canvasY - 50;
                
                LogHelper.LogInfo("MainWindow", 
                    "拖拽放置: DeviceType={DeviceType}, Position=({X},{Y})",
                    deviceType.Name, nodeX, nodeY);
                
                var node = new Models.DeviceNode
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
                
                vm.EditorViewModel.Nodes.Add(node);
                
                LogHelper.LogInfo("MainWindow", "节点创建成功: Id={NodeId}, Title={Title}", node.Id, node.Title);
            }
        }
        else
        {
            LogHelper.LogWarning("MainWindow", "deviceTypeObj 不是 DeviceTypeItem 或 DataContext 不是 MainWindowViewModel");
        }
    }
}
