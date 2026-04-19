using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WaifuAI.Services;

public class SnapshotMessage : ValueChangedMessage<bool>
{
    public SnapshotMessage(bool value) : base(value)
    {
        
    }
}
