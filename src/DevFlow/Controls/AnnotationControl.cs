using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DevFlow.Models;
using DevFlow.Services;

namespace DevFlow.Controls;

public class AnnotationControl : Border
{
    public static readonly StyledProperty<Annotation> AnnotationProperty =
        AvaloniaProperty.Register<AnnotationControl, Annotation>(nameof(Annotation));

    public Annotation Annotation
    {
        get => GetValue(AnnotationProperty);
        set => SetValue(AnnotationProperty, value);
    }

    private readonly TextBox _textBox;
    private bool _isDragging;
    private Point _dragStart;

    public AnnotationControl()
    {
        Background = new SolidColorBrush(Color.Parse("#FFC107"));
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(8);
        MinWidth = 100;
        MinHeight = 50;

        _textBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        
        Child = _textBox;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == AnnotationProperty && change.NewValue is Annotation annotation)
        {
            _textBox.Text = annotation.Text;
            Width = annotation.Width;
            Height = annotation.Height;
            
            try
            {
                Background = new SolidColorBrush(Color.Parse(annotation.BackgroundColor));
                _textBox.Foreground = new SolidColorBrush(Color.Parse(annotation.TextColor));
            }
            catch
            {
                Background = new SolidColorBrush(Color.Parse("#FFC107"));
                _textBox.Foreground = new SolidColorBrush(Color.Parse("#000000"));
            }
            
            _textBox.FontSize = annotation.FontSize;
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && 
            e.Source == this)
        {
            _isDragging = true;
            _dragStart = e.GetPosition(this);
            e.Pointer.Capture(this);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && Annotation != null)
        {
            var parent = this.Parent as Canvas;
            if (parent == null) return;

            var parentPos = e.GetPosition(parent);
            var newX = parentPos.X - _dragStart.X;
            var newY = parentPos.Y - _dragStart.Y;

            Annotation.Position = new Point(newX, newY);
            Canvas.SetLeft(this, newX);
            Canvas.SetTop(this, newY);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            
            LogHelper.LogInfo("AnnotationControl", "注释移动: Id={Id}, Position=({X},{Y})", 
                Annotation?.Id, Annotation?.Position.X, Annotation?.Position.Y);
        }
    }
}
