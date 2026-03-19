using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WaifuAI.Services;

public class ExecuteScriptMessage : ValueChangedMessage<string>
{
    public ExecuteScriptMessage(string value) : base(value)
    {
    }
}