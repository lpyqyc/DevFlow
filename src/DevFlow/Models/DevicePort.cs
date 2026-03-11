using System;

namespace DevFlow.Models;

public class DevicePort
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public Type DataType { get; init; } = typeof(object);
    public object? DefaultValue { get; init; }
    public bool IsRequired { get; init; } = true;
    public PortDirection Direction { get; init; }
}

public enum PortDirection
{
    Input,
    Output,
    Error
}
