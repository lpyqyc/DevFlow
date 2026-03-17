using System;
using System.Text.Json.Serialization;

namespace DevFlow.Models;

/// <summary>
/// 设备端口定义
/// 定义端口的名称、数据类型、方向等属性
/// </summary>
public class DevicePort
{
    #region 标识属性

    /// <summary>
    /// 端口唯一标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    #endregion

    #region 端口属性

    /// <summary>
    /// 端口名称
    /// 显示在节点上
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// 端口数据类型
    /// 用于类型检查和验证
    /// 不序列化，运行时类型信息无法直接序列化
    /// </summary>
    [JsonIgnore]
    public Type DataType { get; init; } = typeof(object);
    
    /// <summary>
    /// 端口数据类型名称
    /// 用于序列化，存储类型的全名
    /// </summary>
    public string? DataTypeName { get; init; }
    
    /// <summary>
    /// 默认值
    /// 用于初始化端口数据
    /// </summary>
    public object? DefaultValue { get; init; }
    
    /// <summary>
    /// 是否必需
    /// 用于连接验证
    /// </summary>
    public bool IsRequired { get; init; } = true;
    
    /// <summary>
    /// 端口方向
    /// 输入、输出或错误
    /// </summary>
    public PortDirection Direction { get; init; }
    
    #endregion
}

/// <summary>
/// 端口方向枚举
/// </summary>
public enum PortDirection
{
    /// <summary>
    /// 输入端口
    /// 位于节点左侧，接收数据
    /// </summary>
    Input,
    
    /// <summary>
    /// 输出端口
    /// 位于节点右侧，输出数据
    /// </summary>
    Output,
    
    /// <summary>
    /// 错误端口
    /// 位于节点右侧下方，输出错误信息
    /// </summary>
    Error
}
