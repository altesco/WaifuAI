using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Threading.Tasks;

namespace WaifuAI.Services;

// Сообщение-запрос: отправляем строку (скрипт), ожидаем Task<object>
public class EvaluateScriptMessage : AsyncRequestMessage<object?>
{
    public string Script { get; }
    public EvaluateScriptMessage(string script)
    {
        Script = script;
    }
}