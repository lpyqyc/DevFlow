using System.Collections.Generic;
using DevFlow.Models.Operations;
using DevFlow.Services;

namespace DevFlow.Services;

public interface IUndoRedoService
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    string? LastOperationDescription { get; }
    
    void ExecuteOperation(IOperation operation);
    void Undo();
    void Redo();
    void Clear();
}

public class UndoRedoService : IUndoRedoService
{
    private readonly Stack<IOperation> _undoStack = new();
    private readonly Stack<IOperation> _redoStack = new();
    private readonly ILogService _logService;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public string? LastOperationDescription => _undoStack.TryPeek(out var op) ? op.Description : null;

    public UndoRedoService(ILogService logService)
    {
        _logService = logService;
    }

    public void ExecuteOperation(IOperation operation)
    {
        _undoStack.Push(operation);
        _redoStack.Clear();
        
        LogHelper.LogInfo("UndoRedo", "操作入栈: {Description}, UndoStack={UndoCount}, RedoStack={RedoCount}",
            operation.Description, _undoStack.Count, _redoStack.Count);
        
        operation.LogDetails(_logService);
    }

    public void Undo()
    {
        if (!_undoStack.TryPop(out var operation))
        {
            LogHelper.LogWarning("UndoRedo", "撤销失败: 没有可撤销的操作");
            return;
        }
        
        operation.Undo();
        _redoStack.Push(operation);
        
        LogHelper.LogInfo("UndoRedo", "撤销: {Description}, UndoStack={UndoCount}, RedoStack={RedoCount}",
            operation.Description, _undoStack.Count, _redoStack.Count);
    }

    public void Redo()
    {
        if (!_redoStack.TryPop(out var operation))
        {
            LogHelper.LogWarning("UndoRedo", "重做失败: 没有可重做的操作");
            return;
        }
        
        operation.Redo();
        _undoStack.Push(operation);
        
        LogHelper.LogInfo("UndoRedo", "重做: {Description}, UndoStack={UndoCount}, RedoStack={RedoCount}",
            operation.Description, _undoStack.Count, _redoStack.Count);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        
        LogHelper.LogInfo("UndoRedo", "历史记录已清空");
    }
}
