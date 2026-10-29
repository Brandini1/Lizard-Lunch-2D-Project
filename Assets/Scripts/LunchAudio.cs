using UnityEngine;
using UnityEngine.InputSystem;

// music gets its own speaker so little effects never interrupt the loop.
public sealed class LunchAudio : MonoBehaviour
{
    private AudioSource music, effects;
    private AudioClip exploring, warning, boss, click, purchase, cleared, victory, defeat;
    private AudioClip nextTrack;
    private bool quiet, muted;

    private void Awake()
    {
        music = gameObject.AddComponent<AudioSource>();
        effects = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = effects.playOnAwake = false;
        music.spatialBlend = effects.spatialBlend = 0f;
        music.loop = true;
        music.volume = 0f;
        effects.volume = 0.55f;
        exploring = Load("Music/secret-base");
        warning = Load("Music/dark-path");
        boss = Load("Music/fated-encounter");
        click = Load("Effects/click");
        purchase = Load("Effects/purchase");
        cleared = Load("Effects/wave-clear");
        victory = Load("Effects/boss-win");
        defeat = Load("Effects/game-over");
    }

    public bool Muted => muted;
    private static AudioClip Load(string name) => Resources.Load<AudioClip>("Audio/" + name);

    public void SetMood(int mood, bool softer)
    {
        nextTrack = mood == 3 ? null : mood == 2 ? boss : mood == 1 ? warning : exploring;
        quiet = softer;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) muted = !muted;
        music.mute = effects.mute = muted;
        // unscaled time lets fades finish in the shop and on the game-over screen.
        float target = nextTrack == music.clip && nextTrack != null ? (quiet ? 0.12f : 0.28f) : 0f;
        music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * 0.6f);
        if (music.clip == nextTrack || music.volume > 0f) return;
        music.Stop();
        music.clip = nextTrack;
        if (nextTrack != null) music.Play();
    }

    private void Play(AudioClip clip)
    {
        if (clip != null) effects.PlayOneShot(clip);
    }

    public void Click() => Play(click);
    public void Purchase() => Play(purchase);
    public void WaveClear() => Play(cleared);
    public void BossWin() => Play(victory);
    public void GameOver() => Play(defeat);
}
