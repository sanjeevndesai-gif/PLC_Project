using CopaFormGui.Models;

namespace CopaFormGui.Services;

public interface ISessionService
{
    SessionRecord? ActiveSession { get; }
    event EventHandler<SessionRecord?>? ActiveSessionChanged;

    void StartSession(string operatorName);
    void EndSession();
    void RecordPunch();
    void RecordProgramRun(string programName);
    List<SessionRecord> GetSessionHistory();
}
