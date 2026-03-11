using System;
using System.Collections.Generic;

namespace DevFlow.Models;

public class DeviceType
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string Icon { get; init; } = "📦";
    public DeviceCategory Category { get; init; }
    public List<DevicePort> InputPorts { get; init; } = new();
    public List<DevicePort> OutputPorts { get; init; } = new();
    public List<DevicePort> ErrorPorts { get; init; } = new();
    public Type? ControlType { get; init; }
}
