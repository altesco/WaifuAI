using System.Threading.Tasks;
using SQLite;
using WaifuAI.Models;
using WaifuAI.ViewModels;

namespace WaifuAI.Services;

public static class DatabaseService
{
    public static readonly KnowledgeDatabase KnowledgeDb = 
        new (SettingsVM.KnowledgeBasePath);

    public static readonly SQLiteAsyncConnection HistoryDb =
        new (SettingsVM.HistoryPath);

    public static async Task InitializeDatabases()
    {
        await KnowledgeDb.CreateTableAsync<KnowledgeRecord>();
        await HistoryDb.CreateTableAsync<Message>();
    }
}
