using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Phonexis.Models;

namespace Phonexis.Services;

public interface ISessionService
{
    bool SessionExists { get; }
    SessionState? Load();
    bool Save(SessionState state);
    void Delete();
}

public class SessionService : ISessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogService _log;

    public SessionService(ILogService log)
    {
        _log = log;
    }

    public bool SessionExists => File.Exists(AppPaths.SessionFile);

    public SessionState? Load()
    {
        try
        {
            if (!SessionExists) return null;
            return JsonSerializer.Deserialize<SessionState>(File.ReadAllText(AppPaths.SessionFile), JsonOptions);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to load session", ex);
            return null;
        }
    }

    public bool Save(SessionState state)
    {
        try
        {
            state.SavedAt = DateTime.Now;
            // Write-then-rename so a crash mid-write cannot corrupt the previous session.
            var tmp = AppPaths.SessionFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(tmp, AppPaths.SessionFile, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Failed to save session", ex);
            return false;
        }
    }

    public void Delete()
    {
        try
        {
            if (SessionExists) File.Delete(AppPaths.SessionFile);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to delete session", ex);
        }
    }
}
