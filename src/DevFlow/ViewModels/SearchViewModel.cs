using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevFlow.Models;
using DevFlow.Services;

namespace DevFlow.ViewModels;

public partial class SearchViewModel : ViewModelBase
{
    private readonly IDeviceRegistry _deviceRegistry;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DeviceTypeItem> _searchResults = new();

    public event EventHandler<DeviceTypeItem>? DeviceSelected;

    public SearchViewModel(IDeviceRegistry deviceRegistry)
    {
        _deviceRegistry = deviceRegistry;
    }

    partial void OnSearchTextChanged(string value)
    {
        UpdateSearchResults();
    }

    private void UpdateSearchResults()
    {
        SearchResults.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        var keyword = SearchText.ToLower();
        var results = _deviceRegistry.GetAllDeviceTypes()
            .Where(d => d.Name.ToLower().Contains(keyword))
            .Select(d => new DeviceTypeItem
            {
                Type = d,
                Icon = d.Icon,
                Name = d.Name
            })
            .ToList();

        foreach (var item in results)
        {
            SearchResults.Add(item);
        }

        LogHelper.LogDebug("SearchViewModel", "搜索: Keyword={Keyword}, Results={Count}", keyword, results.Count);
    }

    [RelayCommand]
    private void SelectDevice(DeviceTypeItem? device)
    {
        if (device == null) return;

        DeviceSelected?.Invoke(this, device);
        IsVisible = false;
        SearchText = string.Empty;

        LogHelper.LogInfo("SearchViewModel", "选择设备: {Name}", device.Name);
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        SearchText = string.Empty;
    }

    public void Toggle()
    {
        IsVisible = !IsVisible;
        if (IsVisible)
        {
            LogHelper.LogInfo("SearchViewModel", "搜索框打开");
        }
    }
}
