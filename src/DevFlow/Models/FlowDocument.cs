using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DevFlow.Models;

/// <summary>
/// 流程文档模型
/// 包含完整的流程定义，支持 JSON 序列化
/// </summary>
public partial class FlowDocument : ObservableObject
{
    #region 版本信息

    /// <summary>
    /// 当前文件格式版本号
    /// 用于向后兼容处理
    /// </summary>
    public const string CurrentVersion = "1.0.0";
    
    /// <summary>
    /// 文件版本号
    /// </summary>
    [ObservableProperty]
    private string _version = CurrentVersion;

    #endregion

    #region 文档标识

    /// <summary>
    /// 流程唯一标识符
    /// </summary>
    public string FlowId { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 流程名称
    /// </summary>
    [ObservableProperty]
    private string _name = "未命名流程";

    #endregion

    #region 时间戳

    /// <summary>
    /// 创建时间
    /// </summary>
    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;
    
    /// <summary>
    /// 最后修改时间
    /// </summary>
    [ObservableProperty]
    private DateTime _modifiedAt = DateTime.Now;

    #endregion

    #region 视图状态

    /// <summary>
    /// 视图状态（缩放和平移）
    /// </summary>
    [ObservableProperty]
    private ViewportState _viewport = new();

    #endregion

    #region 节点数据

    /// <summary>
    /// 节点列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DeviceNode> _nodes = new();

    #endregion

    #region 连接数据

    /// <summary>
    /// 普通连接列表（输出端口连接）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DeviceConnection> _connections = new();
    
    /// <summary>
    /// 错误连接列表（错误端口连接）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DeviceConnection> _errorConnections = new();

    #endregion

    #region 注释数据

    /// <summary>
    /// 注释列表
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Annotation> _annotations = new();

    #endregion
}
