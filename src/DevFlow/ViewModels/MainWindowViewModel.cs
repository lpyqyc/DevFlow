using CommunityToolkit.Mvvm.ComponentModel;
using DevFlow.Services;

namespace DevFlow.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private EditorViewModel _editorViewModel;

    [ObservableProperty]
    private PropertyViewModel _propertyViewModel;

    public MainWindowViewModel(IDeviceRegistry deviceRegistry, IFlowExecutor flowExecutor, IUndoRedoService undoRedoService)
    {
        _editorViewModel = new EditorViewModel(deviceRegistry, flowExecutor, undoRedoService);
        _propertyViewModel = new PropertyViewModel();

        _editorViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EditorViewModel.SelectedNode))
            {
                PropertyViewModel.SelectedNode = _editorViewModel.SelectedNode;
            }
        };
        
        LogHelper.LogInfo("MainWindowViewModel", "MainWindowViewModel 初始化完成");
    }
}
