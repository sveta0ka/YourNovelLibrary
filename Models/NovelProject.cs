using System.Text.Json.Serialization;

namespace YourNovelLibrary.Models;

public sealed class NovelProject
{
    [JsonRequired]
    public string Format { get; set; } = "YourNovelLibrary";

    [JsonRequired]
    public int FormatVersion { get; set; } = 1;

    [JsonRequired]
    public string Title { get; set; } = string.Empty;

    [JsonRequired]
    public List<NovelScene> Scenes { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, byte[]> Assets { get; set; } = new();
    public Dictionary<string, string> AssetNames { get; set; } = new();

    public NovelProgress? ReadingProgress { get; set; }
}

public sealed class NovelScene
{
    [JsonRequired]
    public string Name { get; set; } = string.Empty;

    [JsonRequired]
    public string Text { get; set; } = string.Empty;

    public string Heading { get; set; } = string.Empty;

    public string? BackgroundPath { get; set; }

    public string? CharacterPath { get; set; }

    public string CharacterSide { get; set; } = "left";

    public string? MusicPath { get; set; }
}

public sealed class NovelProgress
{
    [JsonRequired]
    public int SceneIndex { get; set; }

    [JsonRequired]
    public bool IsFinished { get; set; }
}