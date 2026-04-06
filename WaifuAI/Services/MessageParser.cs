using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using ElBruno.LocalEmbeddings;
using WaifuAI.Models;

namespace WaifuAI.Services;

public class MessageParser
{
    public static LocalEmbeddingGenerator VectorGenerator; 

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
        var clean = Regex.Replace(text, @"\*.*?\*", "");
        clean = Regex.Replace(clean, @"\[UPDATE:.*?\]", "");
        return Regex.Replace(clean, @"\s+", " ").Trim();
    }
        
    
}