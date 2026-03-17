namespace DevFlow.Models;

/// <summary>
/// 设备分类枚举
/// 用于对设备类型进行分组
/// </summary>
public enum DeviceCategory
{
    /// <summary>
    /// 传感器
    /// 数据采集设备
    /// </summary>
    Sensor,
    
    /// <summary>
    /// 执行器
    /// 执行动作的设备
    /// </summary>
    Actuator,
    
    /// <summary>
    /// 控制器
    /// 逻辑控制设备
    /// </summary>
    Controller,
    
    /// <summary>
    /// 逻辑
    /// 逻辑运算节点
    /// </summary>
    Logic,
    
    /// <summary>
    /// 输入
    /// 数据输入节点
    /// </summary>
    Input,
    
    /// <summary>
    /// 输出
    /// 数据输出节点
    /// </summary>
    Output,
    
    /// <summary>
    /// 异常处理
    /// 错误处理节点
    /// </summary>
    Exception
}
