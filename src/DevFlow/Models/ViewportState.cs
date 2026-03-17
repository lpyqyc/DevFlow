namespace DevFlow.Models;

/// <summary>
/// 视图状态模型
/// 用于保存和恢复画布的缩放和平移状态
/// </summary>
public class ViewportState
{
    /// <summary>
    /// 缩放比例（1.0 = 100%）
    /// </summary>
    public double Zoom { get; set; } = 1.0;
    
    /// <summary>
    /// X轴平移量（屏幕坐标）
    /// </summary>
    public double TranslateX { get; set; }
    
    /// <summary>
    /// Y轴平移量（屏幕坐标）
    /// </summary>
    public double TranslateY { get; set; }
}
