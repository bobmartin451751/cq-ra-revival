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
        private AudioClip[] _clips;
        private AudioClip _ambientClip;

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
        }

        public static void Play(AudioCue cue)
        {
            if (Instance == null || Instance._source == null)
            {
                return;
            }

            Instance._source.PlayOneShot(Instance._clips[(int)cue]);
        }

        public static void PlayAt(AudioCue cue, Vector3 position)
        {
            if (Instance == null || Instance._source == null)
            {
                return;
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

            if (_ambientClip != null)
            {
                Destroy(_ambientClip);
                _ambientClip = null;
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
