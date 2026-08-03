using CommunityToolkit.Mvvm.Messaging.Messages;

public class EvaluateScriptMessage<T> : AsyncRequestMessage<T>
{
    public string Script { get; }

    public EvaluateScriptMessage(string script)
    {
        Script = script;
    }
}
