using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DevFlow.Controls;
using DevFlow.ViewModels;

namespace DevFlow.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnDeviceItemPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is DeviceTypeItem deviceType)
        {
            var data = new DataObject();
            data.Set("DeviceType", deviceType);
            
            DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
        }
    }

    private void OnCanvasDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("DeviceType"))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnCanvasDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains("DeviceType"))
        {
            var deviceTypeObj = e.Data.Get("DeviceType");
            if (deviceTypeObj is DeviceTypeItem deviceType && DataContext is MainWindowViewModel vm)
            {
                var flowEditor = this.FindControl<FlowEditor>("FlowEditorControl");
                if (flowEditor != null)
                {
                    var position = e.GetPosition(flowEditor);
                    var canvasX = position.X / flowEditor.Zoom;
                    var canvasY = position.Y / flowEditor.Zoom;
                    
                    var node = new Models.DeviceNode
                    {
                        Title = deviceType.Name,
                        DeviceType = deviceType.Type,
                        Position = new Point(canvasX - 90, canvasY - 50)
                    };
                    
                    vm.EditorViewModel.Nodes.Add(node);
                    vm.EditorViewModel.CurrentDocument.Nodes.Add(node);
                    
                    flowEditor.RefreshNodes();
                }
            }
        }
    }
}
