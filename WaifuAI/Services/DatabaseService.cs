using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SQLite;
using WaifuAI.Models;

namespace WaifuAI.Services;

public static class DatabaseService
{
    private static SQLiteAsyncConnection _db;

    public static async Task InitializeDatabase(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<KnowledgeRecord>();
    }

    public static async Task<List<KnowledgeRecord>> GetRecordsAsync() => 
        await _db.Table<KnowledgeRecord>().ToListAsync();

    public static async Task UpdateFavoriteAsync(Guid id, bool isFavorite)
    {
        var record = await _db.Table<KnowledgeRecord>().Where(x => x.Id == id).FirstOrDefaultAsync();
        if (record is null)
            return;
        record.IsFavorite = isFavorite;
        await _db.UpdateAsync(record);
    }

    public static Task SaveRecordAsync(KnowledgeRecord record) => _db.InsertOrReplaceAsync(record);

    public static Task RemoveRecordAsync(KnowledgeRecord record) => _db.DeleteAsync(record);
}
