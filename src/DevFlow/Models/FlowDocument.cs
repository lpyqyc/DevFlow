using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevFlow.Models;

public partial class FlowDocument : ObservableObject
{
    public string FlowId { get; init; } = Guid.NewGuid().ToString();
    
    [ObservableProperty]
    private string _name = "未命名流程";
    
    [ObservableProperty]
    private List<DeviceNode> _nodes = new();
    
    [ObservableProperty]
    private List<DeviceConnection> _connections = new();
    
    [ObservableProperty]
    private List<DeviceConnection> _errorConnections = new();
    
    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;
    
    [ObservableProperty]
    private DateTime _modifiedAt = DateTime.Now;
}
