using UnityEngine;

namespace CQRa.Core
{
    public enum AudioCue : byte
    {
        Click = 0,
        Move = 1,
        Attack = 2,
        Build = 3,
        Explosion = 4,
        Research = 5,
        Victory = 6,
        Defeat = 7,
        AckMove = 8,
        AckAttack = 9,
        AckBuild = 10,
        Announcer = 11,
        Superweapon = 12,
        HeroAbility = 13,
    }

    /// <summary>
    /// Tiny procedural sound layer. No imported audio assets; short mono tones and
    /// noise bursts are generated at scene start and played through one source.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [SerializeField] private float _volume = 0.28f;

        private AudioSource _source;
        private AudioSource _ambientSource;
        private AudioSource _combatSource;
        private AudioClip[] _clips;
        private AudioClip _ambientClip;
        private AudioClip _combatClip;
        private float _combatIntensity;
        private const float CombatBaseVolume = 0.30f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _clips = new[]
            {
                MakeTone("Click", 880f, 0.045f, 0.35f),
                MakeTone("Move", 520f, 0.08f, 0.30f),
                MakeTone("Attack", 220f, 0.10f, 0.38f),
                MakeTone("Build", 660f, 0.16f, 0.30f),
                MakeNoise("Explosion", 0.22f, 0.55f),
                MakeTone("Research", 990f, 0.14f, 0.28f),
                MakeTone("Victory", 740f, 0.35f, 0.32f),
                MakeTone("Defeat", 180f, 0.40f, 0.32f),
                MakeSequence("AckMove", new[] { 560f, 760f }, 0.055f, 0.30f),
                MakeSequence("AckAttack", new[] { 330f, 240f }, 0.06f, 0.34f),
                MakeSequence("AckBuild", new[] { 520f, 700f, 900f }, 0.05f, 0.28f),
                MakeSequence("Announcer", new[] { 1180f, 920f }, 0.045f, 0.26f),
                MakeSequence("Superweapon", new[] { 900f, 500f, 300f, 500f }, 0.09f, 0.36f),
                MakeSequence("HeroAbility", new[] { 700f, 1050f, 1250f }, 0.06f, 0.30f),
            };

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = _volume;

            _ambientClip = MakeAmbience("Ambience", 12f, 0.12f);
            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.clip = _ambientClip;
            _ambientSource.loop = true;
            _ambientSource.playOnAwake = false;
            _ambientSource.spatialBlend = 0f;
            _ambientSource.volume = _volume * 0.35f;
            _ambientSource.Play();

            _combatClip = MakeCombatPulse("CombatPulse", 8f, 0.16f);
            _combatSource = gameObject.AddComponent<AudioSource>();
            _combatSource.clip = _combatClip;
            _combatSource.loop = true;
            _combatSource.playOnAwake = false;
            _combatSource.spatialBlend = 0f;
            _combatSource.volume = _volume * CombatBaseVolume;
        }

        private void Update()
        {
            if (_combatSource == null)
            {
                return;
            }

            _combatIntensity = Mathf.MoveTowards(_combatIntensity, 0f, Time.unscaledDeltaTime * 0.18f);
            if (_combatSource.isPlaying)
            {
                _combatSource.volume = _volume * (CombatBaseVolume + _combatIntensity * 0.22f);
                _combatSource.pitch = 1f + _combatIntensity * 0.12f;
            }
        }

        public static void SetCombat(bool active)
        {
            if (Instance == null || Instance._combatSource == null)
            {
                return;
            }

            if (active && !Instance._combatSource.isPlaying)
            {
                Instance._combatSource.Play();
            }
            else if (!active && Instance._combatSource.isPlaying)
            {
                Instance._combatSource.Stop();
            }
        }

        public static void Play(AudioCue cue)
        {
            if (Instance == null || Instance._source == null)
            {
                return;
            }

            var index = (int)cue;
            if (index < 0 || index >= Instance._clips.Length)
            {
                return;
            }

            Instance._source.PlayOneShot(Instance._clips[index]);
        }

        public static bool HasCue(AudioCue cue)
        {
            return Instance != null &&
                   Instance._clips != null &&
                   (int)cue >= 0 &&
                   (int)cue < Instance._clips.Length &&
                   Instance._clips[(int)cue] != null;
        }

        public static int ClipCount => Instance?._clips?.Length ?? 0;

        public static void PlayAt(AudioCue cue, Vector3 position)
        {
            if (Instance == null || Instance._source == null)
            {
                return;
            }

            if (cue == AudioCue.Explosion)
            {
                Instance._combatIntensity = Mathf.Min(1f, Instance._combatIntensity + 0.22f);
            }

            AudioSource.PlayClipAtPoint(Instance._clips[(int)cue], position, Instance._volume);
        }

        private static AudioClip MakeTone(string name, float frequency, float seconds, float gain)
        {
            const int sampleRate = 44100;
            var samples = Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = Mathf.Sin(Mathf.Clamp01(i / (samples * 0.1f)) * Mathf.PI * 0.5f) *
                               Mathf.Clamp01((samples - i) / (samples * 0.35f));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * gain;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Short radio-style acknowledgement made of decaying notes.</summary>
        private static AudioClip MakeSequence(
            string name,
            float[] frequencies,
            float noteSeconds,
            float gain)
        {
            const int sampleRate = 44100;
            var noteSamples = Mathf.Max(1, Mathf.RoundToInt(noteSeconds * sampleRate));
            var samples = Mathf.Max(1, noteSamples * frequencies.Length);
            var data = new float[samples];

            for (var note = 0; note < frequencies.Length; note++)
            {
                var start = note * noteSamples;
                for (var i = 0; i < noteSamples; i++)
                {
                    var t = i / (float)sampleRate;
                    var envelope = Mathf.Clamp01(i / (noteSamples * 0.08f)) *
                                   Mathf.Pow(1f - i / (float)noteSamples, 1.6f);
                    data[start + i] =
                        Mathf.Sin(2f * Mathf.PI * frequencies[note] * t) * envelope * gain;
                }
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeNoise(string name, float seconds, float gain)
        {
            const int sampleRate = 44100;
            var samples = Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));
            var data = new float[samples];
            var random = new System.Random(991);
            for (var i = 0; i < samples; i++)
            {
                var envelope = Mathf.Pow(1f - i / (float)samples, 2f);
                data[i] = ((float)random.NextDouble() * 2f - 1f) * envelope * gain;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeAmbience(string name, float seconds, float gain)
        {
            const int sampleRate = 22050;
            var samples = Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var slow = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 0.045f * t);
                var drone = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.45f +
                            Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.22f +
                            Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.12f;
                data[i] = drone * slow * gain;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeCombatPulse(string name, float seconds, float gain)
        {
            const int sampleRate = 22050;
            var samples = Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));
            var data = new float[samples];
            var random = new System.Random(557);
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var beat = t % 0.5f;
                var kick = beat < 0.09f
                    ? Mathf.Sin(2f * Mathf.PI * 62f * beat) * (1f - beat / 0.09f)
                    : 0f;
                var tick = beat > 0.25f && beat < 0.29f
                    ? ((float)random.NextDouble() * 2f - 1f) * 0.35f
                    : 0f;
                data[i] = (kick * 0.8f + tick) * gain;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_ambientSource != null)
            {
                _ambientSource.Stop();
            }

            if (_combatSource != null)
            {
                _combatSource.Stop();
            }

            if (_ambientClip != null)
            {
                Destroy(_ambientClip);
                _ambientClip = null;
            }

            if (_combatClip != null)
            {
                Destroy(_combatClip);
                _combatClip = null;
            }

            if (_clips != null)
            {
                foreach (var clip in _clips)
                {
                    if (clip != null)
                    {
                        Destroy(clip);
                    }
                }

                _clips = null;
            }
        }
    }
}
