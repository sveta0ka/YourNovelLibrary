using System.IO;
using System.IO.Compression;
using System.Text.Json;
using YourNovelLibrary.Models;

namespace YourNovelLibrary.Services;

public static class ProjectArchive
{
    public const long MaxFileSize = 50 * 1024 * 1024;

    private const long MaxJsonSize = 5 * 1024 * 1024;
    private const int MaxEntries = 2048;

    private static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public static async Task<MemoryStream> CreateAsync(
        NovelProject project)
    {
        ValidateProject(project);

        if (project.Assets is null
            || project.Assets.Count + 1 > MaxEntries)
        {
            throw new InvalidDataException(
                "В проекте слишком много ресурсов.");
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(
            project, Options);

        if (json.LongLength > MaxJsonSize)
        {
            throw new InvalidDataException(
                "Описание проекта превышает лимит в 5 МБ.");
        }

        long totalSize = json.LongLength;

        foreach (var asset in project.Assets)
        {
            ValidateAssetPath(asset.Key);

            if (asset.Value is null)
            {
                throw new InvalidDataException(
                    "Один из ресурсов проекта не содержит данных.");
            }

            totalSize += asset.Value.LongLength;

            if (totalSize > MaxFileSize)
            {
                throw new InvalidDataException(
                    "Содержимое проекта превышает лимит в 50 МБ.");
            }
        }

        var output = new MemoryStream();

        try
        {
            using (var archive = new ZipArchive(
                output, ZipArchiveMode.Create, leaveOpen: true))
            {
                await WriteEntryAsync(
                    archive, "project.json", json);

                foreach (var asset in project.Assets)
                {
                    await WriteEntryAsync(
                        archive, asset.Key, asset.Value);
                }
            }

            if (output.Length > MaxFileSize)
            {
                throw new InvalidDataException(
                    "Архив проекта превышает лимит в 50 МБ.");
            }

            output.Position = 0;
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    public static async Task<NovelProject> ReadAsync(Stream input)
    {
        var archiveBytes = await ReadLimitedAsync(
            input, MaxFileSize);

        using var buffer = new MemoryStream(
            archiveBytes, writable: false);

        using var archive = new ZipArchive(
            buffer, ZipArchiveMode.Read);

        if (archive.Entries.Count > MaxEntries)
        {
            throw new InvalidDataException(
                "В архиве слишком много файлов.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var assets = new Dictionary<string, byte[]>();

        byte[]? json = null;
        long totalSize = 0;

        foreach (var entry in archive.Entries)
        {
            // Пустые записи каталогов читать не требуется.
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            if (!names.Add(entry.FullName))
            {
                throw new InvalidDataException(
                    "В архиве найдены повторяющиеся имена файлов.");
            }

            var isProjectFile = entry.FullName == "project.json";

            if (!isProjectFile)
            {
                ValidateAssetPath(entry.FullName);
            }

            var limit = Math.Min(
                MaxFileSize - totalSize,
                isProjectFile ? MaxJsonSize : MaxFileSize);

            if (entry.Length > limit)
            {
                throw new InvalidDataException(
                    "Содержимое архива превышает допустимый размер.");
            }

            using var entryStream = entry.Open();

            var bytes = await ReadLimitedAsync(entryStream, limit);
            totalSize += bytes.LongLength;

            if (isProjectFile)
            {
                json = bytes;
            }
            else
            {
                assets.Add(entry.FullName, bytes);
            }
        }

        if (json is null)
        {
            throw new InvalidDataException(
                "В архиве отсутствует project.json.");
        }

        var project = JsonSerializer.Deserialize<NovelProject>(
            json, Options);

        if (project is null)
        {
            throw new InvalidDataException("Проект пуст.");
        }

        project.Assets = assets;

        ValidateProject(project);
        return project;
    }

    private static async Task WriteEntryAsync(
        ZipArchive archive,
        string path,
        byte[] bytes)
    {
        var entry = archive.CreateEntry(
            path, CompressionLevel.Fastest);

        using var stream = entry.Open();
        await stream.WriteAsync(bytes.AsMemory());
    }

    private static async Task<byte[]> ReadLimitedAsync(
        Stream input,
        long limit)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];

        int count;

        while ((count = await input.ReadAsync(
            buffer.AsMemory())) > 0)
        {
            if (output.Length + count > limit)
            {
                throw new InvalidDataException(
                    "Файл превышает допустимый размер.");
            }

            await output.WriteAsync(buffer.AsMemory(0, count));
        }

        return output.ToArray();
    }

    private static void ValidateProject(NovelProject project)
    {
        if (project.Assets is null || project.AssetNames is null)
        {
            throw new InvalidDataException(
                "Некорректная коллекция ресурсов проекта.");
        }

        foreach (var item in project.AssetNames)
        {
            if (!project.Assets.ContainsKey(item.Key)
                || string.IsNullOrWhiteSpace(item.Value)
                || item.Value.Length > 80)
            {
                throw new InvalidDataException(
                    "Некорректное название ресурса проекта.");
            }
        }

        if (project.Format != "YourNovelLibrary")
        {
            throw new InvalidDataException(
                "Неизвестный формат проекта.");
        }

        if (project.FormatVersion != 1)
        {
            throw new InvalidDataException(
                "Версия проекта пока не поддерживается.");
        }

        if (string.IsNullOrWhiteSpace(project.Title)
            || project.Title.Length > 80)
        {
            throw new InvalidDataException(
                "Некорректное название новеллы.");
        }

        if (project.Scenes is null || project.Scenes.Count == 0)
        {
            throw new InvalidDataException(
                "В проекте должна быть хотя бы одна сцена.");
        }

        foreach (var scene in project.Scenes)
        {
            if (scene is null
                || string.IsNullOrWhiteSpace(scene.Name)
                || scene.Name.Length > 80
                || scene.Text is null)
            {
                throw new InvalidDataException(
                    "В проекте найдена некорректная сцена.");
            }
        }
    }

    private static void ValidateAssetPath(string path)
    {
        if (!path.StartsWith("assets/", StringComparison.Ordinal)
            || path.Contains('\\')
            || path.Contains(':')
            || path.Split('/').Any(part =>
                string.IsNullOrWhiteSpace(part)
                || part == "."
                || part == ".."))
        {
            throw new InvalidDataException(
                "Некорректный путь ресурса внутри проекта.");
        }
    }
}