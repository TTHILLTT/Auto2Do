using System.Text.Json;
using System.Text.Json.Serialization;

namespace Auto2Do;

internal sealed class TaskStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Auto2Do", "tasks.json");

    public List<ScheduledTask> Load()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<List<ScheduledTask>>(json, JsonOptions)
            ?? throw new InvalidDataException("任务文件内容为空或格式无效。");
    }

    public void Save(IReadOnlyCollection<ScheduledTask> tasks)
    {
        var directory = Path.GetDirectoryName(FilePath)
            ?? throw new InvalidOperationException("无法确定任务数据目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(tasks, JsonOptions));
        File.Move(temporaryPath, FilePath, overwrite: true);
    }
}
