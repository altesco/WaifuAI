using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace WaifuAI.Services;

public class ScrollMessage : ValueChangedMessage<(int sourceIndex, int replyIndex)>
{
    public ScrollMessage((int sourceIndex, int replyIndex) value) : base(value)
    {
    }
}
