using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DevFlow.Models;

namespace DevFlow.ViewModels;

public partial class PropertyViewModel : ViewModelBase
{
    [ObservableProperty]
    private DeviceNode? _selectedNode;

    [ObservableProperty]
    private ObservableCollection<PropertyItem> _properties = new();

    partial void OnSelectedNodeChanged(DeviceNode? value)
    {
        Properties.Clear();
        if (value == null) return;

        if (value.DeviceType != null)
        {
            foreach (var port in value.DeviceType.InputPorts)
            {
                Properties.Add(new PropertyItem
                {
                    Name = port.Name,
                    Type = port.DataType.Name,
                    Value = value.Properties.TryGetValue(port.Name, out var v) ? v : port.DefaultValue,
                    Port = port
                });
            }

            foreach (var port in value.DeviceType.OutputPorts)
            {
                Properties.Add(new PropertyItem
                {
                    Name = port.Name,
                    Type = port.DataType.Name,
                    Value = value.Properties.TryGetValue(port.Name, out var v) ? v : null,
                    IsReadOnly = true,
                    Port = port
                });
            }
        }

        foreach (var prop in value.Properties)
        {
            if (Properties.All(p => p.Name != prop.Key))
            {
                Properties.Add(new PropertyItem
                {
                    Name = prop.Key,
                    Type = prop.Value?.GetType().Name ?? "object",
                    Value = prop.Value,
                    Port = null
                });
            }
        }
    }

    public void ApplyChanges()
    {
        if (SelectedNode == null) return;

        foreach (var prop in Properties)
        {
            SelectedNode.Properties[prop.Name] = prop.Value;
            
            if (prop.Port != null && prop.Port.Direction == PortDirection.Input)
            {
                SelectedNode.Inputs[prop.Name] = prop.Value;
            }
        }
    }
}

public partial class PropertyItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _type = string.Empty;

    [ObservableProperty]
    private object? _value;

    [ObservableProperty]
    private bool _isReadOnly;

    public DevicePort? Port { get; set; }
}
