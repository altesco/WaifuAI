using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WaifuAI.Services;

public class EvaluateScriptMessage<T> : AsyncRequestMessage<T>
{
    public string Script { get; }

    public EvaluateScriptMessage(string script)
    {
        Script = script;
    }
}

public class ExecuteScriptMessage : ValueChangedMessage<string>
{
    public ExecuteScriptMessage(string value) : base(value)
    {
    }
}

public class ScrollMessage : ValueChangedMessage<(int sourceIndex, int replyIndex)>
{
    public ScrollMessage((int sourceIndex, int replyIndex) value) : base(value)
    {
    }
}

public class SnapshotMessage : ValueChangedMessage<bool>
{
    public SnapshotMessage(bool value) : base(value)
    {
    }
}

public class RequestMessageHeight<T> : RequestMessage<T>
{
    public int Index { get; }

    public RequestMessageHeight(int index)
    {
        Index = index;
    }
}