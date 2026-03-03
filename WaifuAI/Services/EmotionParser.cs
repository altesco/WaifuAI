using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WaifuAI.Models;

namespace WaifuAI.Services;

public class EmotionParser
{
    // Паттерн для поиска *эмоция*
    private static readonly Regex EmotionRegex = new Regex(@"\*(.*?)\*", RegexOptions.Compiled);

    public static ParsedDialogue ParseTextForEmotions(string text)
    {
        var emotions = new List<EmotionInfo>();
        MatchCollection matches = EmotionRegex.Matches(text);
        foreach (Match match in matches)
        {
            emotions.Add(new EmotionInfo
            {
                Name = match.Groups[1].Value,
                OriginalPos = match.Index
            });
        }
        string cleanText = CleanText(text);
        return new ParsedDialogue
        {
            CleanText = cleanText,
            Emotions = emotions
        };
    }

    public static string CleanText(string text)
    {
        string result = EmotionRegex.Replace(text, "");
        result = Regex.Replace(result, @"\s+", " ");
        return result;
    }
        
}