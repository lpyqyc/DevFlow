using System.Collections.ObjectModel;
using Avalonia;
using DevFlow.Models;
using DevFlow.Services;
using DevFlow.ViewModels;

namespace DevFlow.Models.Operations;

public class AddNodeOperation : IOperation
{
    private readonly ObservableCollection<DeviceNode> _nodes;
    private readonly DeviceNode _node;
    private readonly FlowDocument _document;

    public string Description => $"添加节点 [{_node.Title}]";

    public AddNodeOperation(ObservableCollection<DeviceNode> nodes, DeviceNode node, FlowDocument document)
    {
        _nodes = nodes;
        _node = node;
        _document = document;
    }

    public void Undo()
    {
        _nodes.Remove(_node);
        _document.Nodes.Remove(_node);
        LogHelper.LogInfo("UndoRedo", "撤销添加节点: {NodeId}", _node.Id);
    }

    public void Redo()
    {
        _nodes.Add(_node);
        if (!_document.Nodes.Contains(_node))
        {
            _document.Nodes.Add(_node);
        }
        LogHelper.LogInfo("UndoRedo", "重做添加节点: {NodeId}", _node.Id);
    }

    public void LogDetails(ILogService logger)
    {
        logger.Information("操作详情: 添加节点 Id={NodeId}, Title={Title}, Position=({X},{Y})",
            _node.Id, _node.Title, _node.Position.X, _node.Position.Y);
    }
}

public class DeleteNodeOperation : IOperation
{
    private readonly ObservableCollection<DeviceNode> _nodes;
    private readonly DeviceNode _node;
    private readonly FlowDocument _document;
    private readonly int _index;

    public string Description => $"删除节点 [{_node.Title}]";

    public DeleteNodeOperation(ObservableCollection<DeviceNode> nodes, DeviceNode node, FlowDocument document)
    {
        _nodes = nodes;
        _node = node;
        _document = document;
        _index = nodes.IndexOf(node);
    }

    public void Undo()
    {
        if (_index >= 0 && _index <= _nodes.Count)
        {
            _nodes.Insert(_index, _node);
        }
        else
        {
            _nodes.Add(_node);
        }
        
        if (!_document.Nodes.Contains(_node))
        {
            _document.Nodes.Add(_node);
        }
        LogHelper.LogInfo("UndoRedo", "撤销删除节点: {NodeId}", _node.Id);
    }

    public void Redo()
    {
        _nodes.Remove(_node);
        _document.Nodes.Remove(_node);
        LogHelper.LogInfo("UndoRedo", "重做删除节点: {NodeId}", _node.Id);
    }

    public void LogDetails(ILogService logger)
    {
        logger.Information("操作详情: 删除节点 Id={NodeId}, Title={Title}",
            _node.Id, _node.Title);
    }
}

public class MoveNodeOperation : IOperation
{
    private readonly DeviceNode _node;
    private readonly Point _oldPosition;
    private readonly Point _newPosition;

    public string Description => $"移动节点 [{_node.Title}]";

    public MoveNodeOperation(DeviceNode node, Point oldPosition, Point newPosition)
    {
        _node = node;
        _oldPosition = oldPosition;
        _newPosition = newPosition;
    }

    public void Undo()
    {
        _node.Position = _oldPosition;
        LogHelper.LogInfo("UndoRedo", "撤销移动节点: {NodeId}, 位置恢复到 ({X},{Y})", 
            _node.Id, _oldPosition.X, _oldPosition.Y);
    }

    public void Redo()
    {
        _node.Position = _newPosition;
        LogHelper.LogInfo("UndoRedo", "重做移动节点: {NodeId}, 位置移动到 ({X},{Y})", 
            _node.Id, _newPosition.X, _newPosition.Y);
    }

    public void LogDetails(ILogService logger)
    {
        logger.Information("操作详情: 移动节点 Id={NodeId}, 从 ({OX},{OY}) 到 ({NX},{NY})",
            _node.Id, _oldPosition.X, _oldPosition.Y, _newPosition.X, _newPosition.Y);
    }
}

public class AddConnectionOperation : IOperation
{
    private readonly ObservableCollection<ConnectionViewModel> _connections;
    private readonly ConnectionViewModel _connection;
    private readonly FlowDocument _document;
    private readonly DeviceConnection _docConnection;

    public string Description => "添加连接";

    public AddConnectionOperation(
        ObservableCollection<ConnectionViewModel> connections, 
        ConnectionViewModel connection,
        FlowDocument document,
        DeviceConnection docConnection)
    {
        _connections = connections;
        _connection = connection;
        _document = document;
        _docConnection = docConnection;
    }

    public void Undo()
    {
        _connections.Remove(_connection);
        _document.Connections.Remove(_docConnection);
        LogHelper.LogInfo("UndoRedo", "撤销添加连接: {ConnectionId}", _connection.Id);
    }

    public void Redo()
    {
        _connections.Add(_connection);
        if (!_document.Connections.Contains(_docConnection))
        {
            _document.Connections.Add(_docConnection);
        }
        LogHelper.LogInfo("UndoRedo", "重做添加连接: {ConnectionId}", _connection.Id);
    }

    public void LogDetails(ILogService logger)
    {
        logger.Information("操作详情: 添加连接 Id={ConnectionId}, {SourceId}.{SourcePort} -> {TargetId}.{TargetPort}",
            _connection.Id, _connection.SourceNodeId, _connection.SourcePort,
            _connection.TargetNodeId, _connection.TargetPort);
    }
}

public class DeleteConnectionOperation : IOperation
{
    private readonly ObservableCollection<ConnectionViewModel> _connections;
    private readonly ConnectionViewModel _connection;
    private readonly FlowDocument _document;
    private readonly DeviceConnection _docConnection;

    public string Description => "删除连接";

    public DeleteConnectionOperation(
        ObservableCollection<ConnectionViewModel> connections,
        ConnectionViewModel connection,
        FlowDocument document,
        DeviceConnection docConnection)
    {
        _connections = connections;
        _connection = connection;
        _document = document;
        _docConnection = docConnection;
    }

    public void Undo()
    {
        _connections.Add(_connection);
        if (!_document.Connections.Contains(_docConnection))
        {
            _document.Connections.Add(_docConnection);
        }
        LogHelper.LogInfo("UndoRedo", "撤销删除连接: {ConnectionId}", _connection.Id);
    }

    public void Redo()
    {
        _connections.Remove(_connection);
        _document.Connections.Remove(_docConnection);
        LogHelper.LogInfo("UndoRedo", "重做删除连接: {ConnectionId}", _connection.Id);
    }

    public void LogDetails(ILogService logger)
    {
        logger.Information("操作详情: 删除连接 Id={ConnectionId}", _connection.Id);
    }
}
