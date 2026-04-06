using System;
using SQLite;

namespace WaifuAI.Models;

public class KnowledgeRecord
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed]
    public string Key { get; set; }
    
    public string Value { get; set; }
    public string Category { get; set; }
    public bool IsFavorite { get; set; }
    
    [Ignore] 
    public float[]? Vector { get; set; } 

    public string VectorJson 
    { 
        get => Vector != null ? string.Join(",", Vector) : ""; 
        set => Vector = string.IsNullOrEmpty(value) 
                ? null 
                : Array.ConvertAll(value.Split(','), float.Parse);
    }
}