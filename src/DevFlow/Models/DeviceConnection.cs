using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevFlow.Models;

public partial class DeviceConnection : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    [ObservableProperty]
    private string _sourceNodeId = string.Empty;
    
    [ObservableProperty]
    private string _sourcePort = string.Empty;
    
    [ObservableProperty]
    private string _targetNodeId = string.Empty;
    
    [ObservableProperty]
    private string _targetPort = string.Empty;
    
    [ObservableProperty]
    private PortDirection _portDirection = PortDirection.Output;
}
