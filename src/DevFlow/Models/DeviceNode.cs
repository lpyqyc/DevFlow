using System;
using System.Collections.Generic;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevFlow.Models;

public partial class DeviceNode : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    [ObservableProperty]
    private string _title = string.Empty;
    
    [ObservableProperty]
    private DeviceType? _deviceType;
    
    [ObservableProperty]
    private Avalonia.Point _position;
    
    [ObservableProperty]
    private Dictionary<string, object?> _properties = new();
    
    [ObservableProperty]
    private bool _isExecuting;
    
    [ObservableProperty]
    private bool _isCompleted;
    
    [ObservableProperty]
    private bool _isSuccess = true;
    
    [ObservableProperty]
    private bool _hasError;
    
    [ObservableProperty]
    private string? _errorMessage;
    
    public Dictionary<string, object?> Inputs { get; set; } = new();
    public Dictionary<string, object?> Outputs { get; set; } = new();
    public Dictionary<string, object?> Errors { get; set; } = new();
}
