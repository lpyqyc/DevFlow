using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevFlow.Models;
using DevFlow.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IFlowExecutor _flowExecutor;

    [ObservableProperty]
    private FlowDocument _currentDocument = new();

    [ObservableProperty]
    private ObservableCollection<DeviceNode> _nodes = new();

    [ObservableProperty]
    private ObservableCollection<ConnectionViewModel> _connections = new();

    [ObservableProperty]
    private DeviceNode? _selectedNode;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _enableAutoAlign = true;

    public ObservableCollection<DeviceTypeItem> DeviceTypes { get; } = new();

    public EditorViewModel(IDeviceRegistry deviceRegistry, IFlowExecutor flowExecutor)
    {
        _deviceRegistry = deviceRegistry;
        _flowExecutor = flowExecutor;

        LoadDeviceTypes();

        _flowExecutor.DeviceExecuting += OnDeviceExecuting;
        _flowExecutor.DeviceCompleted += OnDeviceCompleted;
        _flowExecutor.DeviceError += OnDeviceError;
        _flowExecutor.FlowCompleted += OnFlowCompleted;
    }

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

    [RelayCommand]
    private void AddNode(DeviceTypeItem? deviceType)
    {
        if (deviceType == null) return;

        var node = new DeviceNode
        {
            Title = deviceType.Name,
            DeviceType = deviceType.Type,
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

        Nodes.Add(node);
        CurrentDocument.Nodes.Add(node);
    }

    [RelayCommand]
    private void DeleteSelectedNode()
    {
        if (SelectedNode == null) return;

        var connectionsToRemove = Connections
            .Where(c => c.SourceNodeId == SelectedNode.Id || c.TargetNodeId == SelectedNode.Id)
            .ToList();

        foreach (var conn in connectionsToRemove)
        {
            Connections.Remove(conn);
            CurrentDocument.Connections.RemoveAll(c => c.Id == conn.Id);
            CurrentDocument.ErrorConnections.RemoveAll(c => c.Id == conn.Id);
        }

        Nodes.Remove(SelectedNode);
        CurrentDocument.Nodes.Remove(SelectedNode);
        SelectedNode = null;
    }

    [RelayCommand]
    private void AutoLayout()
    {
        if (Nodes.Count == 0) return;

        const double startX = 100;
        const double startY = 100;
        const double spacingX = 250;
        const double spacingY = 150;

        var levels = CalculateNodeLevels();
        var maxLevel = levels.Values.Max();

        for (int level = 0; level <= maxLevel; level++)
        {
            var nodesInLevel = levels.Where(kv => kv.Value == level).Select(kv => kv.Key).ToList();
            for (int i = 0; i < nodesInLevel.Count; i++)
            {
                var node = nodesInLevel[i];
                node.Position = new Point(startX + level * spacingX, startY + i * spacingY);
            }
        }
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

    [RelayCommand]
    private void Stop()
    {
        _flowExecutor.Stop();
        IsRunning = false;
    }

    [RelayCommand]
    private async Task SaveAsync(Window? window)
    {
        if (window == null) return;

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存流程",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("流程文件") { Patterns = new[] { "*.json" } }
            },
            DefaultExtension = "json"
        });

        if (file == null) return;

        CurrentDocument.ModifiedAt = DateTime.Now;
        var json = JsonSerializer.Serialize(CurrentDocument, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(file.Path.LocalPath, json);
    }

    [RelayCommand]
    private async Task LoadAsync(Window? window)
    {
        if (window == null) return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "加载流程",
            AllowMultiple = false
        });

        if (files.Count == 0) return;

        var file = files[0];
        var json = await File.ReadAllTextAsync(file.Path.LocalPath);

        var document = JsonSerializer.Deserialize<FlowDocument>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (document == null) return;

        CurrentDocument = document;
        Nodes.Clear();
        Connections.Clear();

        foreach (var node in document.Nodes)
        {
            Nodes.Add(node);
        }

        foreach (var conn in document.Connections)
        {
            Connections.Add(new ConnectionViewModel
            {
                Id = conn.Id,
                SourceNodeId = conn.SourceNodeId,
                TargetNodeId = conn.TargetNodeId,
                SourcePort = conn.SourcePort,
                TargetPort = conn.TargetPort
            });
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        Zoom = Math.Min(Zoom * 1.2, 4.0);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        Zoom = Math.Max(Zoom / 1.2, 0.25);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        Zoom = 1.0;
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

    public void CreateConnection(DeviceNode source, string sourcePort, DeviceNode target, string targetPort, PortDirection direction)
    {
        var connection = new ConnectionViewModel
        {
            Id = Guid.NewGuid().ToString(),
            SourceNodeId = source.Id,
            TargetNodeId = target.Id,
            SourcePort = sourcePort,
            TargetPort = targetPort
        };

        Connections.Add(connection);

        var docConnection = new DeviceConnection
        {
            Id = connection.Id,
            SourceNodeId = connection.SourceNodeId,
            TargetNodeId = connection.TargetNodeId,
            SourcePort = connection.SourcePort,
            TargetPort = connection.TargetPort,
            PortDirection = direction
        };

        if (direction == PortDirection.Error)
        {
            CurrentDocument.ErrorConnections.Add(docConnection);
        }
        else
        {
            CurrentDocument.Connections.Add(docConnection);
        }
    }

    public void RemoveConnection(ConnectionViewModel connection)
    {
        Connections.Remove(connection);
        CurrentDocument.Connections.RemoveAll(c => c.Id == connection.Id);
        CurrentDocument.ErrorConnections.RemoveAll(c => c.Id == connection.Id);
    }
}

public partial class DeviceTypeItem : ObservableObject
{
    [ObservableProperty]
    private DeviceType _type = null!;

    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;
}

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
}
