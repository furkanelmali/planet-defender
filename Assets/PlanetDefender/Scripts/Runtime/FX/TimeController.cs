using DG.Tweening;
using UnityEngine;

namespace PlanetDefender
{
    // Time.timeScale is the product of three independent factors so a hit-stop never un-pauses a level-up
    // and a pause never fights the tutorial slow-motion.
    public static class TimeController
    {
        static float pause = 1f, slow = 1f, hitStop = 1f;
        static Tween pauseTween, slowTween;
        static float hitStopUntil;

        public static bool Frozen => pause <= 0.0001f;

        public static void Freeze(float duration = 0f)
        {
            pauseTween?.Kill();
            if (duration <= 0f) { pause = 0f; Apply(); return; }
            pauseTween = DOTween.To(() => pause, v => { pause = v; Apply(); }, 0f, duration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public static void Unfreeze(float duration = 0f)
        {
            pauseTween?.Kill();
            if (duration <= 0f) { pause = 1f; Apply(); return; }
            pauseTween = DOTween.To(() => pause, v => { pause = v; Apply(); }, 1f, duration).SetEase(Ease.InQuad).SetUpdate(true);
        }

        public static void SlowMotion(float scale, float hold, float recover = 0.35f)
        {
            slowTween?.Kill();
            slow = scale; Apply();
            slowTween = DOTween.Sequence().AppendInterval(hold)
                .Append(DOTween.To(() => slow, v => { slow = v; Apply(); }, 1f, recover))
                .SetUpdate(true);
        }

        // A few frames of near-stop on heavy kills; ignored while frozen.
        public static void HitStop(float seconds, float scale = 0.05f)
        {
            if (Frozen) return;
            hitStop = scale;
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
            Apply();
        }

        public static void Tick()
        {
            if (hitStop < 1f && Time.unscaledTime >= hitStopUntil) { hitStop = 1f; Apply(); }
        }

        public static void ResetAll()
        {
            pauseTween?.Kill(); slowTween?.Kill();
            pause = 1f; slow = 1f; hitStop = 1f; Apply();
        }

        static void Apply() => Time.timeScale = Mathf.Clamp(pause * slow * hitStop, 0f, 1f);
    }
}
