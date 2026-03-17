using System;
using System.Collections.Generic;
using System.Linq;
using DevFlow.Models;
using DevFlow.Services;

namespace DevFlow.Services;

public interface IConnectionValidator
{
    ValidationResult ValidateConnection(
        DeviceNode sourceNode, string sourcePort,
        DeviceNode targetNode, string targetPort,
        IEnumerable<DeviceConnection> existingConnections);
}

public class ConnectionValidator : IConnectionValidator
{
    public ValidationResult ValidateConnection(
        DeviceNode sourceNode, string sourcePort,
        DeviceNode targetNode, string targetPort,
        IEnumerable<DeviceConnection> existingConnections)
    {
        LogHelper.LogDebug("ConnectionValidator", 
            "验证连接: {SourceNode}.{SourcePort} -> {TargetNode}.{TargetPort}",
            sourceNode.Title, sourcePort, targetNode.Title, targetPort);

        if (sourceNode.Id == targetNode.Id)
        {
            LogHelper.LogWarning("ConnectionValidator", "验证失败: 不能连接到自身");
            return ValidationResult.Fail("不能连接到自身");
        }

        var sourcePortInfo = GetPort(sourceNode, sourcePort);
        var targetPortInfo = GetPort(targetNode, targetPort);

        if (sourcePortInfo == null)
        {
            LogHelper.LogWarning("ConnectionValidator", "验证失败: 源端口不存在 {PortName}", sourcePort);
            return ValidationResult.Fail($"源端口 '{sourcePort}' 不存在");
        }

        if (targetPortInfo == null)
        {
            LogHelper.LogWarning("ConnectionValidator", "验证失败: 目标端口不存在 {PortName}", targetPort);
            return ValidationResult.Fail($"目标端口 '{targetPort}' 不存在");
        }

        if (!IsValidDirection(sourcePortInfo.Direction, targetPortInfo.Direction))
        {
            LogHelper.LogInfo("ConnectionValidator", 
                "警告: 端口方向不兼容 SourceDir={SourceDir}, TargetDir={TargetDir}, 但允许连接",
                sourcePortInfo.Direction, targetPortInfo.Direction);
        }

        var existingConnection = existingConnections.FirstOrDefault(c =>
            c.SourceNodeId == sourceNode.Id && c.SourcePort == sourcePort &&
            c.TargetNodeId == targetNode.Id && c.TargetPort == targetPort);
        if (existingConnection != null)
        {
            LogHelper.LogWarning("ConnectionValidator", "验证失败: 连接已存在");
            return ValidationResult.Fail("连接已存在");
        }

        LogHelper.LogInfo("ConnectionValidator", "验证通过");
        return ValidationResult.Success();
    }

    private DevicePort? GetPort(DeviceNode node, string portName)
    {
        if (node.DeviceType == null) return null;

        return node.DeviceType.InputPorts.FirstOrDefault(p => p.Name == portName)
            ?? node.DeviceType.OutputPorts.FirstOrDefault(p => p.Name == portName)
            ?? node.DeviceType.ErrorPorts.FirstOrDefault(p => p.Name == portName);
    }

    private bool IsValidDirection(PortDirection sourceDirection, PortDirection targetDirection)
    {
        return sourceDirection switch
        {
            PortDirection.Output => targetDirection == PortDirection.Input,
            PortDirection.Error => targetDirection == PortDirection.Error || targetDirection == PortDirection.Input,
            PortDirection.Input => false,
            _ => false
        };
    }
}

public class ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Fail(string message) => new(false, message);
}
