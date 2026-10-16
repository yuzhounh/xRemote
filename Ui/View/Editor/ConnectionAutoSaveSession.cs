using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Model.Protocol.Base;
using _1RM.Service.DataSource.DAO;

namespace _1RM.View.Editor;

// Capture on the UI thread, write an independent snapshot, and serialize writes.
// Opening an editor does not write anything; stale completions cannot overwrite newer edits.
internal sealed class ConnectionAutoSaveSession
{
    private readonly Func<ProtocolBase, Result> _persist;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile string _savedFingerprint;
    private Type _savedProtocolType;
    private long _sequence;
    private long _committedSequence;

    internal ConnectionAutoSaveSession(ProtocolBase initial, Func<ProtocolBase, Result> persist)
    {
        _persist = persist;
        _savedFingerprint = Fingerprint(initial.ToJsonString());
        _savedProtocolType = initial.GetType();
    }

    internal bool HasChanges(ProtocolBase edited) => Fingerprint(edited.ToJsonString()) != _savedFingerprint;

    internal Task<Result> SaveAsync(ProtocolBase edited, Func<bool> validate) => SaveCoreAsync(edited, validate, false);

    internal Task<Result> ConfirmProtocolChangeAsync(ProtocolBase edited, Func<bool> validate) => SaveCoreAsync(edited, validate, true);

    private async Task<Result> SaveCoreAsync(ProtocolBase edited, Func<bool> validate, bool confirmProtocolChange)
    {
        var json = edited.ToJsonString();
        var fingerprint = Fingerprint(json);
        var valid = validate();
        var snapshot = edited.CreateFromJsonString(json) ?? throw new InvalidOperationException("Could not capture the connection edit");
        snapshot.Id = edited.Id;
        snapshot.DataSource = edited.DataSource;
        var sequence = Interlocked.Increment(ref _sequence);
        await _gate.WaitAsync();
        try
        {
            // Check inside the gate too: a queued old-protocol save must not undo a confirmed conversion.
            if (!confirmProtocolChange && snapshot.GetType() != _savedProtocolType)
                return Result.Fail("Editor.ProtocolConfirmationRequired");
            if (sequence < _committedSequence || fingerprint == _savedFingerprint) return Result.Success();
            if (!valid) return Result.Fail("Editor.InvalidFields");
            var result = await Task.Run(() => _persist(snapshot));
            if (result.IsSuccess)
            {
                _savedFingerprint = fingerprint;
                _savedProtocolType = snapshot.GetType();
                _committedSequence = sequence;
            }
            return result;
        }
        finally { _gate.Release(); }
    }

    private static string Fingerprint(string value)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
}
