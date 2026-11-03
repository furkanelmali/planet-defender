using System;
using DG.Tweening;
using UnityEngine;

namespace PlanetDefender
{
    public enum Sfx
    {
        Shot, DroneShot, Laser, MeteorHit, Explosion, BigExplosion, PlanetHit, ShieldBlock,
        GoldPickup, GoldShatter, LevelUp, Purchase, Denied, UiClick, Unlock, Countdown, Freeze, Revive, GameOver, NewBest
    }
    public enum Music { None, Menu, Combat, Pressure }

    public sealed class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }
        AudioLibrary library;
        AudioSource[] voices;
        AudioSource musicA, musicB;
        int nextVoice;
        float[] lastPlayed;
        Music current = Music.None;
        AudioLibrary.Cue[] byId;

        public static void Create(AudioLibrary library)
        {
            if (Instance != null) return;
            var go = new GameObject("AudioService");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AudioService>();
            Instance.Init(library);
        }

        void Init(AudioLibrary lib)
        {
            library = lib;
            voices = new AudioSource[14];
            for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
            musicA = gameObject.AddComponent<AudioSource>(); musicB = gameObject.AddComponent<AudioSource>();
            foreach (var m in new[] { musicA, musicB }) { m.loop = true; m.playOnAwake = false; m.volume = 0; m.ignoreListenerPause = false; }
            var ids = Enum.GetValues(typeof(Sfx));
            byId = new AudioLibrary.Cue[ids.Length];
            lastPlayed = new float[ids.Length];
            if (library != null && library.cues != null) foreach (var cue in library.cues) byId[(int)cue.id] = cue;
        }

        static SaveSettings Settings => SaveService.Data.settings;
        public bool SfxMuted { get => Settings.sfxMuted; set { Settings.sfxMuted = value; SaveService.Save(); } }
        public bool MusicMuted
        {
            get => Settings.musicMuted;
            set { Settings.musicMuted = value; SaveService.Save(); ApplyMusicVolume(); }
        }

        public void Play(Sfx id, float volumeScale = 1f, float pitch = 1f)
        {
            if (SfxMuted || byId == null) return;
            var cue = byId[(int)id];
            if (cue == null || cue.clips == null || cue.clips.Length == 0) return;
            float now = Time.unscaledTime;
            if (now - lastPlayed[(int)id] < cue.minGap) return;
            lastPlayed[(int)id] = now;
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.clip = cue.clips[UnityEngine.Random.Range(0, cue.clips.Length)];
            voice.volume = cue.volume * volumeScale;
            voice.pitch = pitch * (1f + UnityEngine.Random.Range(-cue.pitchJitter, cue.pitchJitter));
            voice.Play();
        }

        public void PlayMusic(Music music, float fade = 0.8f)
        {
            if (music == current || library == null) return;
            current = music;
            var clip = music == Music.Menu ? library.menuMusic : music == Music.Combat ? library.combatMusic : music == Music.Pressure ? library.pressureMusic : null;
            var from = musicA.isPlaying ? musicA : musicB;
            var to = from == musicA ? musicB : musicA;
            from.DOKill(); to.DOKill();
            from.DOFade(0, fade).SetUpdate(true).OnComplete(from.Stop);
            if (clip == null) return;
            to.clip = clip; to.volume = 0; to.Play();
            to.DOFade(TargetMusicVolume, fade).SetUpdate(true);
        }

        // Level-up and pause duck the music instead of cutting it; ads silence everything via AudioListener.pause.
        public void Duck(bool ducked)
        {
            var live = musicA.isPlaying ? musicA : musicB;
            live.DOKill();
            live.DOFade(ducked ? TargetMusicVolume * 0.35f : TargetMusicVolume, 0.3f).SetUpdate(true);
        }

        float TargetMusicVolume => MusicMuted || library == null ? 0 : library.musicVolume;
        void ApplyMusicVolume()
        {
            var live = musicA.isPlaying ? musicA : musicB;
            live.DOKill();
            live.volume = TargetMusicVolume;
        }
    }
}
