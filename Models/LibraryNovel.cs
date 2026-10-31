namespace YourNovelLibrary.Models;

public sealed class LibraryNovel
{
    public NovelProject Project { get; init; } = default!;

    public int SceneIndex { get; set; }

    public bool HasStarted { get; set; }

    public bool IsFinished { get; set; }
}