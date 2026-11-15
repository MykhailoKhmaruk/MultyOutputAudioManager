using MultyAudioOutput.Audio;

namespace MultyAudioOutput.Rules;

/// <summary>
/// Keeps saved "app → device" rules applied: on startup, when the app starts new
/// processes, and when the target device (e.g. Bluetooth headphones) comes back.
/// </summary>
internal sealed class RuleEngine
{
    private sealed class State
    {
        public HashSet<uint> Pids = new();
        public string DeviceId = "";
        public bool Applied;
    }

    private readonly AudioService _audio;
    private readonly Func<AppSettings> _settings;
    private readonly Dictionary<string, State> _state = new(StringComparer.OrdinalIgnoreCase);

    public RuleEngine(AudioService audio, Func<AppSettings> settings)
    {
        _audio = audio;
        _settings = settings;
    }

    /// <summary>Records that the app was just routed manually, so the next tick doesn't re-apply.</summary>
    public void MarkApplied(string key, HashSet<uint> pids, string deviceId)
    {
        _state[key] = new State { Pids = pids, DeviceId = deviceId, Applied = true };
    }

    public void Forget(string key) => _state.Remove(key);

    public void Tick(IReadOnlyList<AudioDevice> devices)
    {
        var rules = _settings().Rules;
        var present = devices.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var stale in _state.Keys.Where(k => !rules.ContainsKey(k)).ToList())
            _state.Remove(stale);

        foreach (var (key, rule) in rules)
        {
            if (!_state.TryGetValue(key, out var st))
                _state[key] = st = new State();

            if (!present.Contains(rule.DeviceId))
            {
                // Device unplugged / BT disconnected: Windows falls back to default.
                // Re-apply as soon as it is back.
                st.Applied = false;
                continue;
            }

            var pids = _audio.GetPids(key);
            if (pids.Count == 0)
            {
                st.Pids.Clear();
                continue;
            }

            bool needsApply = !st.Applied
                              || !string.Equals(st.DeviceId, rule.DeviceId, StringComparison.OrdinalIgnoreCase)
                              || !pids.SetEquals(st.Pids);
            if (!needsApply) continue;

            // Only touch processes we haven't routed yet, unless the device changed/returned.
            bool full = !st.Applied || !string.Equals(st.DeviceId, rule.DeviceId, StringComparison.OrdinalIgnoreCase);
            foreach (var pid in pids)
            {
                if (full || !st.Pids.Contains(pid))
                    AudioPolicyConfig.SetDefaultEndpoint(pid, rule.DeviceId);
            }

            st.Pids = pids;
            st.DeviceId = rule.DeviceId;
            st.Applied = true;
        }
    }
}
