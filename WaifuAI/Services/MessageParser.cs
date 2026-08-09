using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using ElBruno.LocalEmbeddings;
using WaifuAI.DTOs;
using WaifuAI.Models;

namespace WaifuAI.Services;

public class MessageParser
{
    public static LocalEmbeddingGenerator VectorGenerator; 

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task CreateVectorGenerator()
    {
        VectorGenerator = await LocalEmbeddingGenerator.CreateAsync();
    }

    public static ParsedDialogue ParseTextForEmotions(string text)
    {
        var emotions = new List<EmotionInfo>();
        var emotionRegex = new Regex(@"\*(.*?)\*", RegexOptions.Compiled);
        MatchCollection matches = emotionRegex.Matches(text);
        int accumulatedOffset = 0;
        foreach (Match match in matches)
        {
            int posInCleanText = match.Index - accumulatedOffset;
            emotions.Add(new EmotionInfo
            {
                Name = match.Groups[1].Value,
                OriginalPos = posInCleanText
            });
            accumulatedOffset += match.Length;
        }
        string cleanText = GetCleanText(text);
        return new ParsedDialogue
        {
            CleanText = cleanText,
            Emotions = emotions
        };
    }

    public static async Task ParseTextForKnowledgeUpdates(string text, ObservableCollection<KnowledgeRecord> knowledgeBase)
    {
        var updateRegex = new Regex(@"\[UPDATE:\s*(.*?)\|(.*?)\|(.*?)\]", RegexOptions.Compiled);
        MatchCollection matches = updateRegex.Matches(text);
        foreach (Match match in matches)
        {
            var key = match.Groups[2].Value;
            var value = match.Groups[3].Value;
            var embedding = await VectorGenerator.GenerateEmbeddingAsync($"{key}: {value}");
            var record = new KnowledgeRecord
            {
                Category = match.Groups[1].Value,
                Key = key,
                Value = value,
                Vector = embedding.Vector.ToArray()
            };
            var existing = knowledgeBase.FirstOrDefault(x => x.Key.Equals(key));
            if (existing != null)
            {
                record.Id = existing.Id;
                record.IsFavorite = existing.IsFavorite;
                await Dispatcher.UIThread.InvokeAsync(() => knowledgeBase.Remove(existing));
            }
            await Dispatcher.UIThread.InvokeAsync(() => knowledgeBase.Add(record));
        }
    }

    public static string GetCleanText(string text)
    {
        var clean = Regex.Replace(text, @"\*.*?\*", "", RegexOptions.Singleline);
        clean = Regex.Replace(clean, @"\[UPDATE:.*?\]", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"```json\s*\{[\s\S]*?\}\s*```", "", RegexOptions.IgnoreCase);

        // если модель не закрыла бэктики ```json
        clean = Regex.Replace(clean, @"\{[\s\S]*?""AffectionDelta""[\s\S]*?\}", "", RegexOptions.IgnoreCase);

        clean = Regex.Replace(clean, @"\[LEARNED_NAME:\s*([^\]]+)\]", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\[RELATIONSHIP:\s*([^\]]+)\]", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\[SLEEP: \s*([^\]]+)\]", "", RegexOptions.IgnoreCase);

        // лишние пробелы
        clean = Regex.Replace(clean, @"[ \t]+", " ");
        clean = Regex.Replace(clean, @"\n\s*\n", "\n\n");

        return clean.Trim();
    }

    public static EmotionDeltasDto? ExtractDeltas(string text)
    {
        var match = Regex.Match(
            text,
            @"```json\s*(\{[\s\S]*?\})\s*```|(\{[\s\S]*?""AffectionDelta""[\s\S]*?\})",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        var jsonString = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

        try
        {
            return JsonSerializer.Deserialize<EmotionDeltasDto>(jsonString, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? ParseTextForLearnedName(string text)
    {
        var match = Regex.Match(text, @"\[LEARNED_NAME:\s*([^\]]+)\]", RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public static bool? ParseTextForDatingChange(string text)
    {
        if (Regex.IsMatch(text, @"\[RELATIONSHIP:\s*DATING_START\]", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(text, @"\[RELATIONSHIP:\s*BREAKUP\]", RegexOptions.IgnoreCase))
            return false;

        return null;
    }

    public static float? ParseWakeUpTime(string text)
    {
        var match = Regex.Match(text, @"\[SLEEP: \s*([^\]]+)\]", RegexOptions.IgnoreCase);

        if (match.Success && 
            float.TryParse(
                match.Groups[1].Value.Trim(), 
                NumberStyles.Float,
                CultureInfo.InvariantCulture, 
                out float result))
            return result;
        return null;
    }
}