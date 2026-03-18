using CopaFormGui.Models;

namespace CopaFormGui.Services;

public class SessionService : ISessionService
{
    private readonly IDataStoreService _dataStoreService;

    public SessionRecord? ActiveSession { get; private set; }
    public event EventHandler<SessionRecord?>? ActiveSessionChanged;

    public SessionService(IDataStoreService dataStoreService)
    {
        _dataStoreService = dataStoreService;
    }

    public void StartSession(string operatorName)
    {
        if (ActiveSession is not null)
            EndSession();

        ActiveSession = new SessionRecord
        {
            SessionId = Guid.NewGuid(),
            OperatorName = string.IsNullOrWhiteSpace(operatorName) ? "Operator" : operatorName.Trim(),
            StartTime = DateTime.Now,
            IsActive = true
        };

        ActiveSessionChanged?.Invoke(this, ActiveSession);
        App.LogInfo($"Session started: {ActiveSession.SessionId} for operator '{ActiveSession.OperatorName}'");
    }

    public void EndSession()
    {
        if (ActiveSession is null) return;

        ActiveSession.EndTime = DateTime.Now;
        ActiveSession.IsActive = false;

        var history = _dataStoreService.LoadSessions();
        history.Add(ActiveSession);
        _dataStoreService.SaveSessions(history);

        App.LogInfo($"Session ended: {ActiveSession.SessionId}, duration: {ActiveSession.DurationText}, punches: {ActiveSession.TotalPunches}");

        ActiveSession = null;
        ActiveSessionChanged?.Invoke(this, null);
    }

    public void RecordPunch()
    {
        if (ActiveSession is not null)
            ActiveSession.TotalPunches++;
    }

    public void RecordProgramRun(string programName)
    {
        if (ActiveSession is not null && !string.IsNullOrWhiteSpace(programName))
        {
            if (!ActiveSession.ProgramsRun.Contains(programName))
                ActiveSession.ProgramsRun.Add(programName);
        }
    }

    public List<SessionRecord> GetSessionHistory()
    {
        return _dataStoreService.LoadSessions();
    }
}
