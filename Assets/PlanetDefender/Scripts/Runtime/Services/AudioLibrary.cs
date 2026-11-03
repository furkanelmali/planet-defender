using System;
using UnityEngine;

namespace PlanetDefender
{
    // Recorded CC0 clips only (no synthesized audio, GDD §13). Built by the editor builder; see CREDITS.md.
    [CreateAssetMenu(menuName = "Planet Defender/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Serializable] public sealed class Cue
        {
            public Sfx id;
            public AudioClip[] clips;
            [Range(0, 1)] public float volume = 1;
            public float pitchJitter = 0.06f;
            public float minGap = 0.03f;   // keeps rapid fire from stacking into a wall of noise
        }
        public Cue[] cues;
        public AudioClip menuMusic, combatMusic, pressureMusic;
        [Range(0, 1)] public float musicVolume = 0.5f;
    }
}
