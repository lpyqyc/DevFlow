using CommunityToolkit.Mvvm.ComponentModel;
using DevFlow.Services;
using DevFlow.ViewModels;

namespace DevFlow.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private EditorViewModel _editorViewModel;

    [ObservableProperty]
    private PropertyViewModel _propertyViewModel;

    public MainWindowViewModel(IDeviceRegistry deviceRegistry, IFlowExecutor flowExecutor)
    {
        _editorViewModel = new EditorViewModel(deviceRegistry, flowExecutor);
        _propertyViewModel = new PropertyViewModel();

        _editorViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EditorViewModel.SelectedNode))
            {
                PropertyViewModel.SelectedNode = _editorViewModel.SelectedNode;
            }
        };
    }
}
