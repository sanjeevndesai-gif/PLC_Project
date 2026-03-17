using CopaFormGui.Models;

namespace CopaFormGui.Services;

public interface IDataStoreService
{
    List<ToolRecord> LoadToolRecords();
    void SaveToolRecords(List<ToolRecord> tools);

    List<PunchProgram> LoadPunchPrograms();
    void SavePunchPrograms(List<PunchProgram> programs);
}
