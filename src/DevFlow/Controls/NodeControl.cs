using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using DevFlow.Models;

namespace DevFlow.Controls;

public class NodeControl : Border
{
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<NodeControl, double>(nameof(Zoom), 1.0);

    public static readonly StyledProperty<Point> TranslateProperty =
        AvaloniaProperty.Register<NodeControl, Point>(nameof(Translate), new Point(0, 0));

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public Point Translate
    {
        get => GetValue(TranslateProperty);
        set => SetValue(TranslateProperty, value);
    }

    private readonly Grid _root = new();
    private readonly StackPanel _inputPorts = new();
    private readonly StackPanel _outputPorts = new();
    private readonly StackPanel _errorPorts = new();
    private readonly TextBlock _titleText = new();
    private readonly TextBlock _deviceTypeText = new();
    private readonly Border _contentArea = new();

    public NodeControl()
    {
        Width = 180;
        Height = 130;
        Background = new SolidColorBrush(Color.Parse("#2D2D30"));
        BorderBrush = new SolidColorBrush(Color.Parse("#3E3E42"));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(0);

        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var headerBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#3E3E42")),
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(4, 4, 0, 0)
        };
        var headerPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        _titleText.FontWeight = Avalonia.Media.FontWeight.SemiBold;
        _titleText.FontSize = 12;
        _titleText.Foreground = Brushes.White;
        _titleText.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        headerPanel.Children.Add(_titleText);
        headerBorder.Child = headerPanel;
        Grid.SetRow(headerBorder, 0);
        _root.Children.Add(headerBorder);

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        _inputPorts.Spacing = 4;
        _inputPorts.Margin = new Thickness(4, 0);
        Grid.SetColumn(_inputPorts, 0);
        contentGrid.Children.Add(_inputPorts);

        _contentArea.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        _contentArea.Padding = new Thickness(4);
        Grid.SetColumn(_contentArea, 1);
        contentGrid.Children.Add(_contentArea);

        var rightPorts = new StackPanel { Spacing = 4, Margin = new Thickness(4, 0) };
        
        _outputPorts.Spacing = 4;
        rightPorts.Children.Add(_outputPorts);
        
        _errorPorts.Spacing = 4;
        rightPorts.Children.Add(_errorPorts);
        
        Grid.SetColumn(rightPorts, 2);
        contentGrid.Children.Add(rightPorts);

        Grid.SetRow(contentGrid, 1);
        _root.Children.Add(contentGrid);

        var footerBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            Padding = new Thickness(8, 2),
            CornerRadius = new CornerRadius(0, 0, 4, 4)
        };
        _deviceTypeText.FontSize = 10;
        _deviceTypeText.Foreground = new SolidColorBrush(Color.Parse("#888888"));
        footerBorder.Child = _deviceTypeText;
        Grid.SetRow(footerBorder, 2);
        _root.Children.Add(footerBorder);

        Child = _root;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is DeviceNode node)
        {
            _titleText.Text = node.Title;
            _deviceTypeText.Text = node.DeviceType?.Name ?? "";

            _inputPorts.Children.Clear();
            _outputPorts.Children.Clear();
            _errorPorts.Children.Clear();

            if (node.DeviceType != null)
            {
                foreach (var port in node.DeviceType.InputPorts)
                {
                    _inputPorts.Children.Add(CreateConnector(port.Name, PortDirection.Input));
                }

                foreach (var port in node.DeviceType.OutputPorts)
                {
                    _outputPorts.Children.Add(CreateConnector(port.Name, PortDirection.Output));
                }

                foreach (var port in node.DeviceType.ErrorPorts)
                {
                    _errorPorts.Children.Add(CreateConnector(port.Name, PortDirection.Error));
                }
            }
        }
    }

    private Border CreateConnector(string name, PortDirection direction)
    {
        var color = direction switch
        {
            PortDirection.Input => Color.Parse("#4CAF50"),
            PortDirection.Output => Color.Parse("#2196F3"),
            PortDirection.Error => Color.Parse("#F44336"),
            _ => Color.Parse("#888888")
        };

        var border = new Border
        {
            Width = 12,
            Height = 12,
            Background = new SolidColorBrush(color),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 2)
        };
        
        ToolTip.SetTip(border, name);
        
        return border;
    }
}
