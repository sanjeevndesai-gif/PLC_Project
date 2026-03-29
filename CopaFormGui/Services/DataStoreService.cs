using System;
using System.IO;
using System.Text.Json;
using CopaFormGui.Models;

namespace CopaFormGui.Services
{
    public class DataStoreService : IDataStoreService
    {
        // Event to notify when tool list changes
        public static event Action? ToolListChanged;
    private static readonly string DataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CopaFormGui");

    private static readonly string ToolsPath = Path.Combine(DataFolder, "tool_records.json");
    private static readonly string ProgramsPath = Path.Combine(DataFolder, "punch_programs.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public List<ToolRecord> LoadToolRecords()
    {
        try
        {
            if (File.Exists(ToolsPath))
            {
                var json = File.ReadAllText(ToolsPath);
                return JsonSerializer.Deserialize<List<ToolRecord>>(json, JsonOptions) ?? new List<ToolRecord>();
            }
        }
        catch
        {
        }

        return new List<ToolRecord>();
    }

    public void SaveToolRecords(List<ToolRecord> tools)
    {
        EnsureFolderExists();
        var json = JsonSerializer.Serialize(tools, JsonOptions);
        File.WriteAllText(ToolsPath, json);
        ToolListChanged?.Invoke();
    }

    public List<PunchProgram> LoadPunchPrograms()
    {
        try
        {
            if (File.Exists(ProgramsPath))
            {
                var json = File.ReadAllText(ProgramsPath);
                return JsonSerializer.Deserialize<List<PunchProgram>>(json, JsonOptions) ?? new List<PunchProgram>();
            }
        }
        catch
        {
        }

        return new List<PunchProgram>();
    }

    public void SavePunchPrograms(List<PunchProgram> programs)
    {
        EnsureFolderExists();
        var json = JsonSerializer.Serialize(programs, JsonOptions);
        File.WriteAllText(ProgramsPath, json);
    }

    private static void EnsureFolderExists()
    {
        if (!Directory.Exists(DataFolder))
            Directory.CreateDirectory(DataFolder);
    }
}
}
