using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevFlow.Models;

/// <summary>
/// 设备连接模型
/// 表示两个节点端口之间的连接关系
/// </summary>
public partial class DeviceConnection : ObservableObject
{
    #region 标识属性

    /// <summary>
    /// 连接唯一标识符
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    #endregion

    #region 连接端点属性

    /// <summary>
    /// 源节点ID
    /// 数据流出的节点
    /// </summary>
    [ObservableProperty]
    private string _sourceNodeId = string.Empty;
    
    /// <summary>
    /// 源端口名称
    /// 数据流出的端口
    /// </summary>
    [ObservableProperty]
    private string _sourcePort = string.Empty;
    
    /// <summary>
    /// 目标节点ID
    /// 数据流入的节点
    /// </summary>
    [ObservableProperty]
    private string _targetNodeId = string.Empty;
    
    /// <summary>
    /// 目标端口名称
    /// 数据流入的端口
    /// </summary>
    [ObservableProperty]
    private string _targetPort = string.Empty;
    
    /// <summary>
    /// 端口方向
    /// 决定连线的颜色和类型
    /// </summary>
    [ObservableProperty]
    private PortDirection _portDirection = PortDirection.Output;
    
    #endregion
}
