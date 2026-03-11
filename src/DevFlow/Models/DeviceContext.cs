using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevFlow.Models;

public partial class DeviceContext : ObservableObject
{
    public string DeviceId { get; set; } = string.Empty;
    
    [ObservableProperty]
    private Dictionary<string, object?> _inputs = new();
    
    [ObservableProperty]
    private Dictionary<string, object?> _outputs = new();
    
    [ObservableProperty]
    private Dictionary<string, object?> _errors = new();
    
    [ObservableProperty]
    private bool _isSuccess = true;
    
    [ObservableProperty]
    private string? _errorMessage;
}
