namespace Naval.App.Services;

/// <summary>
/// Bruitages de la console, sans une ligne de JavaScript : ce service tient la liste des sons
/// en cours, et <c>AudioChannel</c> la matérialise en éléments &lt;audio autoplay&gt; que le
/// navigateur joue à l'insertion. Les volumes sont pré-mixés dans les WAV. Purement décoratif :
/// le jeu ne dépend jamais du son.
/// </summary>
public sealed class AudioService
{
    /// <summary>Un son en cours de lecture. L'identifiant force un élément DOM neuf par tir.</summary>
    public sealed record ActiveSound(long Id, string Name, DateTime StartedUtc);

    private readonly List<ActiveSound> _sounds = [];
    private long _nextId;

    /// <summary>Durée après laquelle un élément audio terminé est retiré du DOM.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(4);

    /// <summary>Nombre maximal d'éléments simultanés, pour borner le DOM.</summary>
    private const int MaxSimultaneous = 6;

    public IReadOnlyList<ActiveSound> Sounds => _sounds;

    /// <summary>État muet, en mémoire pour la session (persister demanderait du JS).</summary>
    public bool Muted { get; private set; }

    public event Action? Changed;

    /// <summary>Déclenche un bruitage par son nom de fichier sans extension (ex. « explosion »).</summary>
    public void Play(string sound)
    {
        if (Muted)
        {
            return;
        }

        var now = DateTime.UtcNow;
        _sounds.RemoveAll(s => now - s.StartedUtc > Lifetime);
        while (_sounds.Count >= MaxSimultaneous)
        {
            _sounds.RemoveAt(0);
        }

        _sounds.Add(new ActiveSound(_nextId++, sound, now));
        Changed?.Invoke();
    }

    public bool ToggleMute()
    {
        Muted = !Muted;
        if (Muted)
        {
            _sounds.Clear();
        }

        Changed?.Invoke();
        return Muted;
    }
}
