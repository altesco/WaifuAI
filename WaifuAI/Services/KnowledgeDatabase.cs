using System;
using System.Threading.Tasks;
using SQLite;
using WaifuAI.Models;

namespace WaifuAI.Services;

public class KnowledgeDatabase : SQLiteAsyncConnection
{
    public KnowledgeDatabase(string databasePath, bool storeDateTimeAsTicks = true) : base(databasePath, storeDateTimeAsTicks)
    {
    }

    public async Task UpdateFavoriteAsync(Guid id, bool isFavorite)
    {
        var record = await Table<KnowledgeRecord>().Where(x => x.Id == id).FirstOrDefaultAsync();
        if (record is null)
            return;
        record.IsFavorite = isFavorite;
        await UpdateAsync(record);
    }
}
