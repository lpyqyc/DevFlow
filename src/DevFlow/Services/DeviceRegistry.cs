using System;
using System.Collections.Generic;
using DevFlow.Models;

namespace DevFlow.Services;

public interface IDeviceRegistry
{
    IReadOnlyList<DeviceType> GetAllDeviceTypes();
    DeviceType? GetDeviceType(string id);
    void RegisterDeviceType(DeviceType deviceType);
}

public class DeviceRegistry : IDeviceRegistry
{
    private readonly Dictionary<string, DeviceType> _deviceTypes = new();

    public DeviceRegistry()
    {
        RegisterDefaultDevices();
    }

    private void RegisterDefaultDevices()
    {
        RegisterDeviceType(new DeviceType
        {
            Id = "Camera",
            Name = "摄像头",
            Icon = "📷",
            Category = DeviceCategory.Sensor,
            InputPorts = new List<DevicePort>(),
            OutputPorts = new List<DevicePort>
            {
                new() { Name = "图像数据", DataType = typeof(byte[]), Direction = PortDirection.Output }
            },
            ErrorPorts = new List<DevicePort>
            {
                new() { Name = "错误", DataType = typeof(string), Direction = PortDirection.Error }
            }
        });

        RegisterDeviceType(new DeviceType
        {
            Id = "Motor",
            Name = "电机",
            Icon = "⚙️",
            Category = DeviceCategory.Actuator,
            InputPorts = new List<DevicePort>
            {
                new() { Name = "速度控制", DataType = typeof(int), Direction = PortDirection.Input }
            },
            OutputPorts = new List<DevicePort>
            {
                new() { Name = "运行状态", DataType = typeof(bool), Direction = PortDirection.Output }
            },
            ErrorPorts = new List<DevicePort>
            {
                new() { Name = "错误", DataType = typeof(string), Direction = PortDirection.Error }
            }
        });

        RegisterDeviceType(new DeviceType
        {
            Id = "Sensor",
            Name = "传感器",
            Icon = "🌡️",
            Category = DeviceCategory.Sensor,
            InputPorts = new List<DevicePort>(),
            OutputPorts = new List<DevicePort>
            {
                new() { Name = "传感器数据", DataType = typeof(double), Direction = PortDirection.Output }
            },
            ErrorPorts = new List<DevicePort>
            {
                new() { Name = "错误", DataType = typeof(string), Direction = PortDirection.Error }
            }
        });

        RegisterDeviceType(new DeviceType
        {
            Id = "Switch",
            Name = "开关",
            Icon = "🔌",
            Category = DeviceCategory.Controller,
            InputPorts = new List<DevicePort>
            {
                new() { Name = "控制信号", DataType = typeof(bool), Direction = PortDirection.Input }
            },
            OutputPorts = new List<DevicePort>
            {
                new() { Name = "开关状态", DataType = typeof(bool), Direction = PortDirection.Output }
            },
            ErrorPorts = new List<DevicePort>
            {
                new() { Name = "错误", DataType = typeof(string), Direction = PortDirection.Error }
            }
        });

        RegisterDeviceType(new DeviceType
        {
            Id = "ExceptionHandler",
            Name = "异常处理",
            Icon = "⚠️",
            Category = DeviceCategory.Exception,
            InputPorts = new List<DevicePort>
            {
                new() { Name = "正常输入", DataType = typeof(object), Direction = PortDirection.Input }
            },
            OutputPorts = new List<DevicePort>
            {
                new() { Name = "降级输出", DataType = typeof(object), Direction = PortDirection.Output }
            },
            ErrorPorts = new List<DevicePort>
            {
                new() { Name = "错误输入", DataType = typeof(string), Direction = PortDirection.Error }
            }
        });
    }

    public IReadOnlyList<DeviceType> GetAllDeviceTypes() => new List<DeviceType>(_deviceTypes.Values);

    public DeviceType? GetDeviceType(string id) => _deviceTypes.TryGetValue(id, out var type) ? type : null;

    public void RegisterDeviceType(DeviceType deviceType)
    {
        _deviceTypes[deviceType.Id] = deviceType;
    }
}
