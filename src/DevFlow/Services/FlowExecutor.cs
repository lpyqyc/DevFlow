using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevFlow.Models;

namespace DevFlow.Services;

public class FlowExecutor : IFlowExecutor
{
    private readonly IDeviceRegistry _deviceRegistry;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly Dictionary<string, List<Action>> _eventTriggers = new();
    private readonly Dictionary<string, List<DeviceNode>> _executionQueue = new();

    public event EventHandler<DeviceNode>? DeviceExecuting;
    public event EventHandler<DeviceNode>? DeviceCompleted;
    public event EventHandler<(DeviceNode Node, string Error)>? DeviceError;
    public event EventHandler? FlowCompleted;
    public event EventHandler? FlowStopped;

    public bool IsRunning { get; private set; }

    public FlowExecutor(IDeviceRegistry deviceRegistry)
    {
        _deviceRegistry = deviceRegistry;
    }

    public async Task ExecuteAsync(FlowDocument document)
    {
        if (IsRunning) return;

        IsRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var orderedNodes = TopologicalSort(document.Nodes, document.Connections);

            foreach (var node in orderedNodes)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                await ExecuteNodeAsync(node, document);
            }

            FlowCompleted?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task ExecuteNodeAsync(DeviceNode node, FlowDocument document)
    {
        node.IsExecuting = true;
        node.IsCompleted = false;
        node.HasError = false;
        DeviceExecuting?.Invoke(this, node);

        try
        {
            var context = new DeviceContext
            {
                DeviceId = node.Id
            };

            CollectInputs(node, document, context);

            await SimulateExecutionAsync(node, context);

            node.Outputs = context.Outputs;
            node.Errors = context.Errors;
            node.IsSuccess = context.IsSuccess;
            node.IsCompleted = true;

            if (!context.IsSuccess)
            {
                node.HasError = true;
                node.ErrorMessage = context.ErrorMessage;
                await RouteToErrorHandlerAsync(node, document);
            }

            DeviceCompleted?.Invoke(this, node);
        }
        catch (Exception ex)
        {
            node.HasError = true;
            node.ErrorMessage = ex.Message;
            DeviceError?.Invoke(this, (node, ex.Message));
            await RouteToErrorHandlerAsync(node, document);
        }
        finally
        {
            node.IsExecuting = false;
        }
    }

    private void CollectInputs(DeviceNode node, FlowDocument document, DeviceContext context)
    {
        var inputConnections = document.Connections
            .Where(c => c.TargetNodeId == node.Id)
            .ToList();

        foreach (var conn in inputConnections)
        {
            var sourceNode = document.Nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId);
            if (sourceNode != null && sourceNode.Outputs.TryGetValue(conn.SourcePort, out var value))
            {
                context.Inputs[conn.TargetPort] = value;
            }
        }

        var errorConnections = document.ErrorConnections
            .Where(c => c.TargetNodeId == node.Id)
            .ToList();

        foreach (var conn in errorConnections)
        {
            var sourceNode = document.Nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId);
            if (sourceNode != null)
            {
                foreach (var error in sourceNode.Errors)
                {
                    context.Errors[error.Key] = error.Value;
                }
            }
        }
    }

    private Task SimulateExecutionAsync(DeviceNode node, DeviceContext context)
    {
        return Task.Run(() =>
        {
            Thread.Sleep(500);

            context.Outputs = new Dictionary<string, object?>
            {
                { "result", $"Processed by {node.Title}" }
            };
            context.IsSuccess = true;
        });
    }

    private async Task RouteToErrorHandlerAsync(DeviceNode node, FlowDocument document)
    {
        var errorHandlerConnections = document.ErrorConnections
            .Where(c => c.SourceNodeId == node.Id)
            .ToList();

        foreach (var conn in errorHandlerConnections)
        {
            var handlerNode = document.Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
            if (handlerNode != null)
            {
                await ExecuteNodeAsync(handlerNode, document);
            }
        }
    }

    private List<DeviceNode> TopologicalSort(IEnumerable<DeviceNode> nodes, IEnumerable<DeviceConnection> connections)
    {
        var result = new List<DeviceNode>();
        var visited = new HashSet<string>();
        var nodeMap = nodes.ToDictionary(n => n.Id);

        void Visit(DeviceNode node)
        {
            if (visited.Contains(node.Id)) return;
            visited.Add(node.Id);

            var dependencies = connections
                .Where(c => c.TargetNodeId == node.Id)
                .Select(c => nodeMap.TryGetValue(c.SourceNodeId, out var dep) ? dep : null)
                .Where(n => n != null)
                .Cast<DeviceNode>();

            foreach (var dep in dependencies)
            {
                Visit(dep);
            }

            result.Add(node);
        }

        foreach (var node in nodeMap.Values)
        {
            Visit(node);
        }

        return result;
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        FlowStopped?.Invoke(this, EventArgs.Empty);
        IsRunning = false;
    }

    public void RegisterEventTrigger(string eventName, Action trigger)
    {
        if (!_eventTriggers.ContainsKey(eventName))
        {
            _eventTriggers[eventName] = new List<Action>();
        }
        _eventTriggers[eventName].Add(trigger);
    }

    public void TriggerEvent(string eventName)
    {
        if (_eventTriggers.TryGetValue(eventName, out var triggers))
        {
            foreach (var trigger in triggers)
            {
                trigger();
            }
        }
    }
}
