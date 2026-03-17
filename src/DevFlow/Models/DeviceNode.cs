using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevFlow.Models;

/// <summary>
/// 设备节点模型
/// 表示流程图中的一个设备节点
/// 包含节点的位置、状态、输入/输出/错误端口数据
/// </summary>
public partial class DeviceNode : ObservableObject
{
    #region 标识属性

    /// <summary>
    /// 节点唯一标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    #endregion

    #region 基本属性

    /// <summary>
    /// 节点标题
    /// 显示在节点标题栏
    /// </summary>
    [ObservableProperty]
    private string _title = string.Empty;
    
    /// <summary>
    /// 设备类型定义
    /// 定义节点的端口和属性
    /// 不序列化，运行时从 DeviceRegistry 获取
    /// </summary>
    [property: JsonIgnore]
    [ObservableProperty]
    private DeviceType? _deviceType;
    
    /// <summary>
    /// 设备类型ID
    /// 用于序列化和反序列化时恢复 DeviceType
    /// </summary>
    [ObservableProperty]
    private string? _deviceTypeId;
    
    #endregion

    #region 位置属性

    /// <summary>
    /// 节点位置（文档坐标）
    /// 存储在画布上的逻辑位置，不随缩放变化
    /// </summary>
    private Avalonia.Point _position;
    
    /// <summary>
    /// 节点位置（文档坐标）
    /// 用于运行时的位置访问
    /// </summary>
    [property: JsonIgnore]
    public Avalonia.Point Position
    {
        get => _position;
        set
        {
            SetProperty(ref _position, value);
            OnPropertyChanged(nameof(PositionX));
            OnPropertyChanged(nameof(PositionY));
        }
    }
    
    /// <summary>
    /// 节点X坐标（用于序列化）
    /// </summary>
    public double PositionX
    {
        get => _position.X;
        set
        {
            _position = new Point(value, _position.Y);
            OnPropertyChanged(nameof(Position));
        }
    }
    
    /// <summary>
    /// 节点Y坐标（用于序列化）
    /// </summary>
    public double PositionY
    {
        get => _position.Y;
        set
        {
            _position = new Point(_position.X, value);
            OnPropertyChanged(nameof(Position));
        }
    }
    
    #endregion

    #region 自定义属性

    /// <summary>
    /// 自定义属性字典
    /// 存储用户设置的节点属性值
    /// </summary>
    [ObservableProperty]
    private Dictionary<string, object?> _properties = new();
    
    #endregion

    #region 运行时状态

    /// <summary>
    /// 是否正在执行
    /// 运行时状态，不序列化
    /// </summary>
    [property: JsonIgnore]
    [ObservableProperty]
    private bool _isExecuting;
    
    /// <summary>
    /// 是否执行完成
    /// 运行时状态，不序列化
    /// </summary>
    [property: JsonIgnore]
    [ObservableProperty]
    private bool _isCompleted;
    
    /// <summary>
    /// 是否执行成功
    /// 运行时状态，不序列化
    /// </summary>
    [property: JsonIgnore]
    [ObservableProperty]
    private bool _isSuccess = true;
    
    /// <summary>
    /// 是否有错误
    /// 运行时状态，不序列化
    /// </summary>
    [property: JsonIgnore]
    [ObservableProperty]
    private bool _hasError;
    
    /// <summary>
    /// 错误消息
    /// 运行时状态，不序列化
    /// </summary>
    [property: JsonIgnore]
    [ObservableProperty]
    private string? _errorMessage;
    
    #endregion

    #region 端口数据

    /// <summary>
    /// 输入端口数据
    /// 键为端口名称，值为端口数据
    /// </summary>
    public Dictionary<string, object?> Inputs { get; set; } = new();
    
    /// <summary>
    /// 输出端口数据
    /// 键为端口名称，值为端口数据
    /// </summary>
    public Dictionary<string, object?> Outputs { get; set; } = new();
    
    /// <summary>
    /// 错误端口数据
    /// 键为端口名称，值为错误信息
    /// </summary>
    public Dictionary<string, object?> Errors { get; set; } = new();
    
    #endregion
}
