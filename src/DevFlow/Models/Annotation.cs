using System;
using System.Text.Json.Serialization;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevFlow.Models;

/// <summary>
/// 注释模型
/// 用于在画布上添加文字注释
/// </summary>
public partial class Annotation : ObservableObject
{
    #region 标识属性

    /// <summary>
    /// 注释唯一标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    #endregion

    #region 位置属性（支持序列化）

    /// <summary>
    /// 注释位置（运行时使用）
    /// </summary>
    [JsonIgnore]
    public Point Position
    {
        get => new Point(PositionX, PositionY);
        set
        {
            PositionX = value.X;
            PositionY = value.Y;
        }
    }

    /// <summary>
    /// X坐标（用于 JSON 序列化）
    /// </summary>
    [ObservableProperty]
    private double _positionX;

    /// <summary>
    /// Y坐标（用于 JSON 序列化）
    /// </summary>
    [ObservableProperty]
    private double _positionY;

    #endregion

    #region 尺寸属性

    /// <summary>
    /// 宽度
    /// </summary>
    [ObservableProperty]
    private double _width = 200;
    
    /// <summary>
    /// 高度
    /// </summary>
    [ObservableProperty]
    private double _height = 100;

    #endregion

    #region 内容属性

    /// <summary>
    /// 注释文本内容
    /// </summary>
    [ObservableProperty]
    private string _text = string.Empty;

    #endregion

    #region 样式属性

    /// <summary>
    /// 背景颜色（十六进制格式）
    /// </summary>
    [ObservableProperty]
    private string _backgroundColor = "#FFC107";
    
    /// <summary>
    /// 文字颜色（十六进制格式）
    /// </summary>
    [ObservableProperty]
    private string _textColor = "#000000";
    
    /// <summary>
    /// 字体大小
    /// </summary>
    [ObservableProperty]
    private double _fontSize = 12;

    #endregion
}
