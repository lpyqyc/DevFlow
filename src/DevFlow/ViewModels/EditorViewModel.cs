using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevFlow.Controls;
using DevFlow.Models;
using DevFlow.Models.Operations;
using DevFlow.Services;

namespace DevFlow.ViewModels;

/// <summary>
/// 编辑器视图模型
/// 管理流程文档的编辑、保存、加载等功能
/// </summary>
public partial class EditorViewModel : ViewModelBase
{
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IFlowExecutor _flowExecutor;
    private readonly IUndoRedoService _undoRedoService;

    /// <summary>
    /// 当前流程文档
    /// </summary>
    [ObservableProperty]
    private FlowDocument _currentDocument = new();

    /// <summary>
    /// 节点集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DeviceNode> _nodes = new();

    /// <summary>
    /// 连接集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ConnectionViewModel> _connections = new();

    /// <summary>
    /// 注释集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Annotation> _annotations = new();

    /// <summary>
    /// 当前选中的节点
    /// </summary>
    [ObservableProperty]
    private DeviceNode? _selectedNode;

    /// <summary>
    /// 缩放比例
    /// </summary>
    [ObservableProperty]
    private double _zoom = 1.0;

    /// <summary>
    /// X轴平移量
    /// </summary>
    [ObservableProperty]
    private double _translateX;

    /// <summary>
    /// Y轴平移量
    /// </summary>
    [ObservableProperty]
    private double _translateY;

    /// <summary>
    /// 是否正在执行流程
    /// </summary>
    [ObservableProperty]
    private bool _isRunning;

    /// <summary>
    /// 是否显示网格
    /// </summary>
    [ObservableProperty]
    private bool _showGrid = true;

    /// <summary>
    /// 是否启用自动对齐
    /// </summary>
    [ObservableProperty]
    private bool _enableAutoAlign = true;

    /// <summary>
    /// 是否可以撤销
    /// </summary>
    [ObservableProperty]
    private bool _canUndo;

    /// <summary>
    /// 是否可以重做
    /// </summary>
    [ObservableProperty]
    private bool _canRedo;
    
    /// <summary>
    /// 当前打开的文件路径
    /// </summary>
    [ObservableProperty]
    private string? _currentFilePath;
    
    /// <summary>
    /// 文档是否有未保存的更改
    /// </summary>
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>
    /// 连线刷新请求事件
    /// </summary>
    public event EventHandler? ConnectionsRefreshRequested;
    
    /// <summary>
    /// 视图状态变化事件（用于通知 FlowEditor 更新）
    /// </summary>
    public event EventHandler<ViewportState>? ViewportChanged;
    
    /// <summary>
    /// 新文档创建事件
    /// </summary>
    public event EventHandler? DocumentReset;

    /// <summary>
    /// 设备类型列表
    /// </summary>
    public ObservableCollection<DeviceTypeItem> DeviceTypes { get; } = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    public EditorViewModel(IDeviceRegistry deviceRegistry, IFlowExecutor flowExecutor, IUndoRedoService undoRedoService)
    {
        _deviceRegistry = deviceRegistry;
        _flowExecutor = flowExecutor;
        _undoRedoService = undoRedoService;

        LoadDeviceTypes();

        _flowExecutor.DeviceExecuting += OnDeviceExecuting;
        _flowExecutor.DeviceCompleted += OnDeviceCompleted;
        _flowExecutor.DeviceError += OnDeviceError;
        _flowExecutor.FlowCompleted += OnFlowCompleted;
        
        Nodes.CollectionChanged += OnNodesCollectionChanged;
        Connections.CollectionChanged += OnConnectionsCollectionChanged;
        
        LogHelper.LogInfo("EditorViewModel", "EditorViewModel 初始化完成");
    }

    /// <summary>
    /// 加载设备类型列表
    /// </summary>
    private void LoadDeviceTypes()
    {
        foreach (var type in _deviceRegistry.GetAllDeviceTypes())
        {
            DeviceTypes.Add(new DeviceTypeItem
            {
                Type = type,
                Icon = type.Icon,
                Name = type.Name
            });
        }
    }

    #region 集合变化处理

    private void OnNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (DeviceNode node in e.NewItems)
            {
                if (!CurrentDocument.Nodes.Contains(node))
                {
                    CurrentDocument.Nodes.Add(node);
                }
            }
        }
        
        if (e.OldItems != null)
        {
            foreach (DeviceNode node in e.OldItems)
            {
                CurrentDocument.Nodes.Remove(node);
            }
        }
        
        HasUnsavedChanges = true;
    }
    
    private void OnConnectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (ConnectionViewModel conn in e.NewItems)
            {
                var existingConn = CurrentDocument.Connections.FirstOrDefault(c => c.Id == conn.Id);
                if (existingConn == null)
                {
                    var docConn = new DeviceConnection
                    {
                        Id = conn.Id,
                        SourceNodeId = conn.SourceNodeId,
                        TargetNodeId = conn.TargetNodeId,
                        SourcePort = conn.SourcePort,
                        TargetPort = conn.TargetPort,
                        PortDirection = conn.Direction
                    };
                    
                    if (conn.Direction == PortDirection.Error)
                    {
                        CurrentDocument.ErrorConnections.Add(docConn);
                    }
                    else
                    {
                        CurrentDocument.Connections.Add(docConn);
                    }
                }
            }
        }
        
        if (e.OldItems != null)
        {
            foreach (ConnectionViewModel conn in e.OldItems)
            {
                CurrentDocument.Connections.RemoveAll(c => c.Id == conn.Id);
                CurrentDocument.ErrorConnections.RemoveAll(c => c.Id == conn.Id);
            }
        }
        
        HasUnsavedChanges = true;
    }

    #endregion

    #region 节点操作

    /// <summary>
    /// 添加节点命令
    /// </summary>
    [RelayCommand]
    private void AddNode(DeviceTypeItem? deviceType)
    {
        if (deviceType == null) return;

        var node = new DeviceNode
        {
            Title = deviceType.Name,
            DeviceType = deviceType.Type,
            DeviceTypeId = deviceType.Type.Id,
            Position = new Point(200 + Nodes.Count * 50, 200 + Nodes.Count * 50)
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

        var operation = new AddNodeOperation(Nodes, node, CurrentDocument);
        _undoRedoService.ExecuteOperation(operation);
        
        Nodes.Add(node);
        CurrentDocument.Nodes.Add(node);
        
        UpdateUndoRedoState();
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// 删除选中节点命令
    /// </summary>
    [RelayCommand]
    private void DeleteSelectedNode()
    {
        if (SelectedNode == null) return;

        var connectionsToRemove = Connections
            .Where(c => c.SourceNodeId == SelectedNode.Id || c.TargetNodeId == SelectedNode.Id)
            .ToList();

        foreach (var conn in connectionsToRemove)
        {
            var docConn = CurrentDocument.Connections.FirstOrDefault(c => c.Id == conn.Id);
            if (docConn != null)
            {
                var deleteConnOp = new DeleteConnectionOperation(Connections, conn, CurrentDocument, docConn);
                _undoRedoService.ExecuteOperation(deleteConnOp);
            }
            Connections.Remove(conn);
            CurrentDocument.Connections.RemoveAll(c => c.Id == conn.Id);
            CurrentDocument.ErrorConnections.RemoveAll(c => c.Id == conn.Id);
        }

        var operation = new DeleteNodeOperation(Nodes, SelectedNode, CurrentDocument);
        _undoRedoService.ExecuteOperation(operation);
        
        Nodes.Remove(SelectedNode);
        CurrentDocument.Nodes.Remove(SelectedNode);
        SelectedNode = null;
        
        UpdateUndoRedoState();
        HasUnsavedChanges = true;
    }

    #endregion

    #region 撤销/重做

    /// <summary>
    /// 撤销命令
    /// </summary>
    [RelayCommand]
    private void Undo()
    {
        _undoRedoService.Undo();
        UpdateUndoRedoState();
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// 重做命令
    /// </summary>
    [RelayCommand]
    private void Redo()
    {
        _undoRedoService.Redo();
        UpdateUndoRedoState();
        HasUnsavedChanges = true;
    }

    private void UpdateUndoRedoState()
    {
        CanUndo = _undoRedoService.CanUndo;
        CanRedo = _undoRedoService.CanRedo;
    }

    #endregion

    #region 自动布局

    /// <summary>
    /// 自动布局命令
    /// </summary>
    [RelayCommand]
    private void AutoLayout()
    {
        if (Nodes.Count == 0) return;

        const double nodeWidth = 180;
        const double nodeHeight = 130;
        const double horizontalSpacing = 80;
        const double verticalSpacing = 40;
        const double startX = 100;
        const double startY = 100;

        var levels = CalculateNodeLevels();
        var maxLevel = levels.Values.Max();

        var levelNodeCounts = new Dictionary<int, int>();
        foreach (var kv in levels)
        {
            if (!levelNodeCounts.ContainsKey(kv.Value))
                levelNodeCounts[kv.Value] = 0;
            levelNodeCounts[kv.Value]++;
        }

        var levelCurrentIndices = new Dictionary<int, int>();
        foreach (var level in levelNodeCounts.Keys)
        {
            levelCurrentIndices[level] = 0;
        }

        for (int level = 0; level <= maxLevel; level++)
        {
            var nodesInLevel = levels.Where(kv => kv.Value == level).Select(kv => kv.Key).ToList();
            var totalHeight = nodesInLevel.Count * nodeHeight + (nodesInLevel.Count - 1) * verticalSpacing;
            var currentY = startY;

            for (int i = 0; i < nodesInLevel.Count; i++)
            {
                var node = nodesInLevel[i];
                var x = startX + level * (nodeWidth + horizontalSpacing);
                node.Position = new Point(x, currentY);
                currentY += nodeHeight + verticalSpacing;
            }
        }
        
        ConnectionsRefreshRequested?.Invoke(this, EventArgs.Empty);
        HasUnsavedChanges = true;
    }

    private Dictionary<DeviceNode, int> CalculateNodeLevels()
    {
        var levels = new Dictionary<DeviceNode, int>();
        var visited = new HashSet<DeviceNode>();

        foreach (var node in Nodes)
        {
            CalculateLevel(node, levels, visited);
        }

        return levels;
    }

    private int CalculateLevel(DeviceNode node, Dictionary<DeviceNode, int> levels, HashSet<DeviceNode> visited)
    {
        if (levels.TryGetValue(node, out var existingLevel))
            return existingLevel;

        if (visited.Contains(node))
            return 0;

        visited.Add(node);

        var incomingConnections = CurrentDocument.Connections
            .Where(c => c.TargetNodeId == node.Id)
            .ToList();

        if (incomingConnections.Count == 0)
        {
            levels[node] = 0;
            return 0;
        }

        var maxLevel = 0;
        foreach (var conn in incomingConnections)
        {
            var sourceNode = Nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId);
            if (sourceNode != null)
            {
                var level = CalculateLevel(sourceNode, levels, visited);
                maxLevel = Math.Max(maxLevel, level);
            }
        }

        levels[node] = maxLevel + 1;
        return maxLevel + 1;
    }

    #endregion

    #region 流程执行

    /// <summary>
    /// 执行流程命令
    /// </summary>
    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (IsRunning) return;
        IsRunning = true;

        foreach (var node in Nodes)
        {
            node.IsExecuting = false;
            node.IsCompleted = false;
            node.HasError = false;
            node.ErrorMessage = null;
        }

        await _flowExecutor.ExecuteAsync(CurrentDocument);
    }

    /// <summary>
    /// 停止执行命令
    /// </summary>
    [RelayCommand]
    private void Stop()
    {
        _flowExecutor.Stop();
        IsRunning = false;
    }

    private void OnDeviceExecuting(object? sender, DeviceNode node)
    {
        node.IsExecuting = true;
        node.IsCompleted = false;
    }

    private void OnDeviceCompleted(object? sender, DeviceNode node)
    {
        node.IsExecuting = false;
        node.IsCompleted = true;
    }

    private void OnDeviceError(object? sender, (DeviceNode Node, string Error) e)
    {
        e.Node.IsExecuting = false;
        e.Node.HasError = true;
        e.Node.ErrorMessage = e.Error;
    }

    private void OnFlowCompleted(object? sender, EventArgs e)
    {
        IsRunning = false;
    }

    #endregion

    #region 文件操作

    /// <summary>
    /// 新建流程命令
    /// </summary>
    [RelayCommand]
    private void New()
    {
        // 检查是否有未保存的更改
        if (HasUnsavedChanges)
        {
            // TODO: 提示用户保存
        }

        // 清空当前文档
        Nodes.CollectionChanged -= OnNodesCollectionChanged;
        Connections.CollectionChanged -= OnConnectionsCollectionChanged;
        
        Nodes.Clear();
        Connections.Clear();
        Annotations.Clear();
        _undoRedoService.Clear();

        // 创建新文档
        CurrentDocument = new FlowDocument();
        CurrentFilePath = null;
        HasUnsavedChanges = false;
        
        // 重置视图状态
        Zoom = 1.0;
        TranslateX = 0;
        TranslateY = 0;
        
        Nodes.CollectionChanged += OnNodesCollectionChanged;
        Connections.CollectionChanged += OnConnectionsCollectionChanged;
        
        // 触发文档重置事件
        DocumentReset?.Invoke(this, EventArgs.Empty);
        
        UpdateUndoRedoState();
        
        LogHelper.LogInfo("EditorViewModel", "新建流程文档");
    }

    /// <summary>
    /// 保存流程命令
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync(Window? window)
    {
        if (window == null)
        {
            LogHelper.LogWarning("EditorViewModel", "保存失败: Window 为 null");
            return;
        }

        try
        {
            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "保存流程",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("流程文件") { Patterns = new[] { "*.json" } }
                },
                DefaultExtension = "json",
                SuggestedFileName = CurrentDocument.Name
            });

            if (file == null)
            {
                LogHelper.LogInfo("EditorViewModel", "保存取消: 用户未选择文件");
                return;
            }

            await SaveToFileAsync(file.Path.LocalPath);
            CurrentFilePath = file.Path.LocalPath;
        }
        catch (Exception ex)
        {
            LogHelper.LogError("EditorViewModel", "保存失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 保存到指定文件
    /// </summary>
    private async Task SaveToFileAsync(string filePath)
    {
        // 更新视图状态
        CurrentDocument.Viewport = new ViewportState
        {
            Zoom = Zoom,
            TranslateX = TranslateX,
            TranslateY = TranslateY
        };
        
        // 更新注释列表
        CurrentDocument.Annotations = Annotations.ToList();
        
        // 更新时间戳
        CurrentDocument.ModifiedAt = DateTime.Now;
        
        // 序列化
        var json = JsonSerializer.Serialize(CurrentDocument, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(filePath, json);
        HasUnsavedChanges = false;
        
        LogHelper.LogInfo("EditorViewModel", "流程已保存: {FilePath}", filePath);
    }

    /// <summary>
    /// 加载流程命令
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync(Window? window)
    {
        if (window == null) return;

        try
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "加载流程",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("流程文件") { Patterns = new[] { "*.json" } }
                }
            });

            if (files.Count == 0) return;

            var file = files[0];
            await LoadFromFileAsync(file.Path.LocalPath);
            CurrentFilePath = file.Path.LocalPath;
        }
        catch (Exception ex)
        {
            LogHelper.LogError("EditorViewModel", "加载流程失败: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// 从指定文件加载
    /// </summary>
    private async Task LoadFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);

        var document = JsonSerializer.Deserialize<FlowDocument>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (document == null)
        {
            LogHelper.LogWarning("EditorViewModel", "加载失败: 无法解析流程文件");
            return;
        }

        // 取消集合变化监听
        Nodes.CollectionChanged -= OnNodesCollectionChanged;
        Connections.CollectionChanged -= OnConnectionsCollectionChanged;
        
        // 设置当前文档
        CurrentDocument = document;
        Nodes.Clear();
        Connections.Clear();
        Annotations.Clear();
        _undoRedoService.Clear();

        // 恢复节点
        foreach (var node in document.Nodes)
        {
            if (!string.IsNullOrEmpty(node.DeviceTypeId))
            {
                node.DeviceType = _deviceRegistry.GetDeviceType(node.DeviceTypeId);
            }
            Nodes.Add(node);
        }

        // 恢复连接
        foreach (var conn in document.Connections)
        {
            Connections.Add(new ConnectionViewModel
            {
                Id = conn.Id,
                SourceNodeId = conn.SourceNodeId,
                TargetNodeId = conn.TargetNodeId,
                SourcePort = conn.SourcePort,
                TargetPort = conn.TargetPort,
                Direction = conn.PortDirection
            });
        }
        
        // 恢复错误连接
        foreach (var conn in document.ErrorConnections)
        {
            Connections.Add(new ConnectionViewModel
            {
                Id = conn.Id,
                SourceNodeId = conn.SourceNodeId,
                TargetNodeId = conn.TargetNodeId,
                SourcePort = conn.SourcePort,
                TargetPort = conn.TargetPort,
                Direction = PortDirection.Error
            });
        }
        
        // 恢复注释
        if (document.Annotations != null)
        {
            foreach (var annotation in document.Annotations)
            {
                Annotations.Add(annotation);
            }
        }
        
        // 恢复集合变化监听
        Nodes.CollectionChanged += OnNodesCollectionChanged;
        Connections.CollectionChanged += OnConnectionsCollectionChanged;
        
        // 恢复视图状态
        if (document.Viewport != null)
        {
            Zoom = document.Viewport.Zoom;
            TranslateX = document.Viewport.TranslateX;
            TranslateY = document.Viewport.TranslateY;
            
            // 通知 FlowEditor 更新视图状态
            ViewportChanged?.Invoke(this, document.Viewport);
        }
        
        // 触发连线刷新
        ConnectionsRefreshRequested?.Invoke(this, EventArgs.Empty);
        
        HasUnsavedChanges = false;
        
        LogHelper.LogInfo("EditorViewModel", "流程已加载: {FilePath}, 节点数={NodeCount}, 连接数={ConnectionCount}", 
            filePath, Nodes.Count, Connections.Count);
        
        UpdateUndoRedoState();
    }

    #endregion

    #region 缩放控制

    /// <summary>
    /// 放大命令
    /// </summary>
    [RelayCommand]
    private void ZoomIn()
    {
        Zoom = Math.Min(Zoom * 1.2, 4.0);
    }

    /// <summary>
    /// 缩小命令
    /// </summary>
    [RelayCommand]
    private void ZoomOut()
    {
        Zoom = Math.Max(Zoom / 1.2, 0.25);
    }

    /// <summary>
    /// 重置缩放命令
    /// </summary>
    [RelayCommand]
    private void ResetZoom()
    {
        Zoom = 1.0;
        TranslateX = 0;
        TranslateY = 0;
    }

    #endregion

    #region 连接管理

    /// <summary>
    /// 创建连接
    /// </summary>
    public void CreateConnection(DeviceNode source, string sourcePort, DeviceNode target, string targetPort, PortDirection direction)
    {
        var connection = new ConnectionViewModel
        {
            Id = Guid.NewGuid().ToString(),
            SourceNodeId = source.Id,
            TargetNodeId = target.Id,
            SourcePort = sourcePort,
            TargetPort = targetPort,
            Direction = direction
        };

        var docConnection = new DeviceConnection
        {
            Id = connection.Id,
            SourceNodeId = connection.SourceNodeId,
            TargetNodeId = connection.TargetNodeId,
            SourcePort = connection.SourcePort,
            TargetPort = connection.TargetPort,
            PortDirection = direction
        };

        var operation = new AddConnectionOperation(Connections, connection, CurrentDocument, docConnection);
        _undoRedoService.ExecuteOperation(operation);

        Connections.Add(connection);

        if (direction == PortDirection.Error)
        {
            CurrentDocument.ErrorConnections.Add(docConnection);
        }
        else
        {
            CurrentDocument.Connections.Add(docConnection);
        }
        
        UpdateUndoRedoState();
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// 删除连接
    /// </summary>
    public void RemoveConnection(ConnectionViewModel connection)
    {
        var docConn = CurrentDocument.Connections.FirstOrDefault(c => c.Id == connection.Id);
        if (docConn == null)
        {
            docConn = CurrentDocument.ErrorConnections.FirstOrDefault(c => c.Id == connection.Id);
        }
        
        if (docConn != null)
        {
            var operation = new DeleteConnectionOperation(Connections, connection, CurrentDocument, docConn);
            _undoRedoService.ExecuteOperation(operation);
        }
        
        Connections.Remove(connection);
        CurrentDocument.Connections.RemoveAll(c => c.Id == connection.Id);
        CurrentDocument.ErrorConnections.RemoveAll(c => c.Id == connection.Id);
        
        UpdateUndoRedoState();
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// 从 FlowEditor 更新视图状态
    /// </summary>
    public void UpdateViewportState(double zoom, double translateX, double translateY)
    {
        Zoom = zoom;
        TranslateX = translateX;
        TranslateY = translateY;
    }

    #endregion
}

/// <summary>
/// 设备类型项
/// 用于设备列表显示
/// </summary>
public partial class DeviceTypeItem : ObservableObject
{
    [ObservableProperty]
    private DeviceType _type = null!;

    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;
}

/// <summary>
/// 连接视图模型
/// </summary>
public partial class ConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _sourceNodeId = string.Empty;

    [ObservableProperty]
    private string _targetNodeId = string.Empty;

    [ObservableProperty]
    private string _sourcePort = string.Empty;

    [ObservableProperty]
    private string _targetPort = string.Empty;

    [ObservableProperty]
    private PortDirection _direction = PortDirection.Output;
}
