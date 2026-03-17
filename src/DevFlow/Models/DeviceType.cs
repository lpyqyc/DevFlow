using System;
using System.Collections.Generic;

namespace DevFlow.Models;

/// <summary>
/// 设备类型定义
/// 定义一类设备的端口结构、分类、图标等元数据
/// </summary>
public class DeviceType
{
    #region 标识属性

    /// <summary>
    /// 设备类型唯一标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    #endregion

    #region 基本属性

    /// <summary>
    /// 设备类型名称
    /// 显示在设备列表中
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// 设备图标
    /// Emoji 或图标名称
    /// </summary>
    public string Icon { get; init; } = "📦";
    
    /// <summary>
    /// 设备分类
    /// 用于分组显示
    /// </summary>
    public DeviceCategory Category { get; init; }
    
    #endregion

    #region 端口定义

    /// <summary>
    /// 输入端口列表
    /// 定义节点左侧的端口
    /// </summary>
    public List<DevicePort> InputPorts { get; init; } = new();
    
    /// <summary>
    /// 输出端口列表
    /// 定义节点右侧的端口
    /// </summary>
    public List<DevicePort> OutputPorts { get; init; } = new();
    
    /// <summary>
    /// 错误端口列表
    /// 定义节点右侧下方的错误处理端口
    /// </summary>
    public List<DevicePort> ErrorPorts { get; init; } = new();
    
    #endregion

    #region 控件类型

    /// <summary>
    /// 自定义控件类型
    /// 用于显示节点的自定义UI
    /// </summary>
    public Type? ControlType { get; init; }
    
    #endregion
}
