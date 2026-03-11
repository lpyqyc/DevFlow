using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevFlow.Models;

namespace DevFlow.Services;

public interface IFlowExecutor
{
    event EventHandler<DeviceNode>? DeviceExecuting;
    event EventHandler<DeviceNode>? DeviceCompleted;
    event EventHandler<(DeviceNode Node, string Error)>? DeviceError;
    event EventHandler? FlowCompleted;
    event EventHandler? FlowStopped;

    bool IsRunning { get; }

    Task ExecuteAsync(FlowDocument document);
    void Stop();
    void RegisterEventTrigger(string eventName, Action trigger);
    void TriggerEvent(string eventName);
}
