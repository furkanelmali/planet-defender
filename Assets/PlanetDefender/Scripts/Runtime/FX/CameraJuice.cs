using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PlanetDefender
{
    // Screen shake, zoom kicks and post-processing pulses. The camera sits under a rig so shake offsets never
    // accumulate with the framing done by ArenaCamera.
    public sealed class CameraJuice : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        Transform shaker;
        Vignette vignette;
        ChromaticAberration chroma;
        Bloom bloom;
        float baseVignette, baseBloom;
        Color baseVignetteColor;
        float baseSize;
        Tween shake, zoom, vignetteTween, chromaTween, bloomTween;

        public void Init(Camera camera, VolumeProfile profile)
        {
            Camera = camera;
            shaker = camera.transform;
            if (profile != null)
            {
                profile.TryGet(out vignette); profile.TryGet(out chroma); profile.TryGet(out bloom);
                if (vignette) { baseVignette = vignette.intensity.value; baseVignetteColor = vignette.color.value; }
                if (bloom) baseBloom = bloom.intensity.value;
                if (chroma) chroma.intensity.value = 0;
            }
        }

        public void SetBaseSize(float size) { baseSize = size; if (zoom == null || !zoom.IsActive()) Camera.orthographicSize = size; }

        public void Shake(float strength, float duration = 0.25f, int vibrato = 22)
        {
            if (shake != null && shake.IsActive()) shake.Complete();
            shaker.localPosition = new Vector3(0, 0, shaker.localPosition.z);
            shake = shaker.DOShakePosition(duration, new Vector3(strength, strength, 0), vibrato, 90, false, true, ShakeRandomnessMode.Harmonic);
        }

        // Brief zoom-in punch on heavy events; returns to the framed size.
        public void Kick(float amount = 0.25f, float duration = 0.28f)
        {
            zoom?.Kill();
            Camera.orthographicSize = baseSize;
            zoom = Camera.DOOrthoSize(baseSize - amount, duration * 0.3f).SetEase(Ease.OutQuad)
                .OnComplete(() => zoom = Camera.DOOrthoSize(baseSize, duration * 0.7f).SetEase(Ease.OutBack));
        }

        public void DamageFlash()
        {
            if (!vignette) return;
            vignetteTween?.Kill();
            vignette.color.value = Palette.Danger;
            vignette.intensity.value = 0.5f;
            vignetteTween = DOTween.To(() => vignette.intensity.value, v => vignette.intensity.value = v, baseVignette, 0.6f)
                .SetEase(Ease.OutQuad).OnComplete(() => vignette.color.value = baseVignetteColor);
            Chroma(0.6f, 0.35f);
        }

        // Low life: a slow red breathing at the frame edge until healed or the run ends.
        public void LowHealth(bool on)
        {
            if (!vignette) return;
            vignetteTween?.Kill();
            if (!on) { vignette.color.value = baseVignetteColor; vignette.intensity.value = baseVignette; return; }
            vignette.color.value = Palette.Danger;
            vignette.intensity.value = baseVignette;
            vignetteTween = DOTween.To(() => vignette.intensity.value, v => vignette.intensity.value = v, 0.42f, 0.7f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }

        public void Chroma(float amount, float duration)
        {
            if (!chroma) return;
            chromaTween?.Kill();
            chroma.intensity.value = amount;
            chromaTween = DOTween.To(() => chroma.intensity.value, v => chroma.intensity.value = v, 0f, duration).SetUpdate(true);
        }

        public void BloomPulse(float extra, float duration)
        {
            if (!bloom) return;
            bloomTween?.Kill();
            bloom.intensity.value = baseBloom + extra;
            bloomTween = DOTween.To(() => bloom.intensity.value, v => bloom.intensity.value = v, baseBloom, duration).SetUpdate(true);
        }

        public void ResetEffects()
        {
            vignetteTween?.Kill(); chromaTween?.Kill(); bloomTween?.Kill();
            if (vignette) { vignette.intensity.value = baseVignette; vignette.color.value = baseVignetteColor; }
            if (chroma) chroma.intensity.value = 0;
            if (bloom) bloom.intensity.value = baseBloom;
        }

        void OnDestroy() => ResetEffects(); // the profile is an asset; leave it as authored
    }
}
