using System.Collections.Generic;

namespace WaifuAI.Models;

public class ParsedDialogue
{
    public string CleanText { get; set; }
    public List<EmotionInfo> Emotions { get; set; }
}