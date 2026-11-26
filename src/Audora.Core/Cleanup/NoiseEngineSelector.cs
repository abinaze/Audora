namespace Audora.Core.Cleanup;

/// <summary>
/// Hands out the denoiser for one cleaner. If Studio is wanted but its files are missing or it fails to start, it
/// falls back to Standard once, remembers that, and says why in plain words. After Studio has started it never
/// switches, because two channels with different delays would be out of step. Give <see cref="Create"/> to a cleaner as its factory.
/// </summary>
public sealed class NoiseEngineSelector
{
    private readonly object _gate = new();
    private readonly bool _wantsStudio;
    private readonly Func<IFrameDenoiser>? _studio;
    private readonly Func<IFrameDenoiser> _standard;
    private string? _decided;
    private string? _reason;

    public NoiseEngineSelector(string requestedEngine, Func<IFrameDenoiser>? studioFactory, Func<IFrameDenoiser> standardFactory, string? studioUnavailableReason = null)
    {
        ArgumentNullException.ThrowIfNull(standardFactory);
        _wantsStudio = NoiseEngines.Normalize(requestedEngine) == NoiseEngines.Studio;
        _studio = studioFactory;
        _standard = standardFactory;

        if (!_wantsStudio)
        {
            _decided = NoiseEngines.Standard;
        }
        else if (studioFactory is null)
        {
            _decided = NoiseEngines.Standard;
            _reason = studioUnavailableReason ?? "The Studio files were not found, so Standard noise removal is used.";
        }
    }

    /// <summary>The engine in use, or the one that will be tried first.</summary>
    public string ActiveEngine
    {
        get
        {
            lock (_gate)
            {
                return _decided ?? NoiseEngines.Studio;
            }
        }
    }

    /// <summary>A plain sentence when Studio was wanted but Standard is used instead. Null otherwise.</summary>
    public string? FallbackReason
    {
        get
        {
            lock (_gate)
            {
                return _reason;
            }
        }
    }

    /// <summary>The selector for a microphone: Studio from the "studio" folder beside the app, and RNNoise as the fallback.</summary>
    public static NoiseEngineSelector ForMicrophone(string requestedEngine, string appFolder)
    {
        var files = StudioFiles.Locate(StudioFiles.FolderIn(appFolder));
        if (files.HasValue)
        {
            var library = files.Value.Library;
            var model = files.Value.Model;
            return new NoiseEngineSelector(requestedEngine, () => DeepFilterDenoiser.Create(library, model), () => new RnNoiseDenoiser());
        }

        return new NoiseEngineSelector(
            requestedEngine, null, () => new RnNoiseDenoiser(), "The Studio files are not installed, so Standard noise removal is used.");
    }

    public IFrameDenoiser Create()
    {
        lock (_gate)
        {
            if (_decided == NoiseEngines.Standard)
            {
                return _standard();
            }

            try
            {
                var denoiser = _studio!();
                _decided = NoiseEngines.Studio;
                return denoiser;
            }
            catch (Exception ex) when (_decided is null)
            {
                _decided = NoiseEngines.Standard;
                _reason = $"Studio noise removal could not start ({Describe(ex)}), so Standard noise removal is used.";
                return _standard();
            }
        }
    }

    private static string Describe(Exception ex)
    {
        var text = ex.Message?.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(text) ? ex.GetType().Name : text;
    }
}
