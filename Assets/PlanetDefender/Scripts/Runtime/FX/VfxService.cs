using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace PlanetDefender
{
    // Pooled particle bursts and floating combat text. Prefabs come from GameAssets (built by the editor builder).
    public sealed class VfxService : MonoBehaviour
    {
        readonly Dictionary<ParticleSystem, Stack<ParticleSystem>> pools = new Dictionary<ParticleSystem, Stack<ParticleSystem>>();
        readonly List<(ParticleSystem instance, ParticleSystem prefab, float until)> live = new List<(ParticleSystem, ParticleSystem, float)>();
        readonly Stack<TextMeshPro> texts = new Stack<TextMeshPro>();
        TMP_FontAsset font;

        public void Init(TMP_FontAsset font) { this.font = font; }

        public ParticleSystem Play(ParticleSystem prefab, Vector3 position, float scale = 1f, Color? tint = null, float rotation = 0f)
        {
            if (prefab == null) return null;
            if (!pools.TryGetValue(prefab, out var stack)) pools[prefab] = stack = new Stack<ParticleSystem>();
            var ps = stack.Count > 0 ? stack.Pop() : Instantiate(prefab, transform);
            ps.gameObject.SetActive(true);
            ps.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, rotation));
            ps.transform.localScale = Vector3.one * scale;
            if (tint.HasValue)
            {
                var main = ps.main;
                main.startColor = tint.Value;
            }
            ps.Clear(true);
            ps.Play(true);
            float life = ps.main.duration + ps.main.startLifetime.constantMax + 0.2f;
            live.Add((ps, prefab, Time.time + life));
            return ps;
        }

        void Update()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var entry = live[i];
                if (Time.time < entry.until && entry.instance.IsAlive(true)) continue;
                entry.instance.gameObject.SetActive(false);
                pools[entry.prefab].Push(entry.instance);
                live.RemoveAt(i);
            }
        }

        public void ClearAll()
        {
            foreach (var entry in live) { entry.instance.Clear(true); entry.instance.gameObject.SetActive(false); pools[entry.prefab].Push(entry.instance); }
            live.Clear();
        }

        // World-space popup: punch in, drift up, fade. Runs on scaled time so it freezes with the game.
        public void Text(Vector3 position, string message, Color color, float size = 4.2f, float rise = 0.9f, float duration = 0.8f)
        {
            var text = texts.Count > 0 ? texts.Pop() : CreateText();
            text.gameObject.SetActive(true);
            text.text = message;
            text.alpha = 1f;
            text.color = color;
            text.fontSize = size;
            var t = text.transform;
            t.DOKill(); text.DOKill();
            t.position = position + new Vector3(Random.Range(-0.15f, 0.15f), 0.2f, 0);
            t.localScale = Vector3.one * 0.4f;
            var seq = DOTween.Sequence().SetTarget(t);
            seq.Append(t.DOScale(1.15f, 0.12f).SetEase(Ease.OutBack));
            seq.Append(t.DOScale(1f, 0.08f));
            seq.Insert(0, t.DOMoveY(t.position.y + rise, duration).SetEase(Ease.OutCubic));
            seq.Insert(duration * 0.55f, text.Fade(0, duration * 0.45f));
            seq.OnComplete(() => { text.gameObject.SetActive(false); texts.Push(text); });
        }

        TextMeshPro CreateText()
        {
            var go = new GameObject("FloatText");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            text.font = font;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontSharedMaterial = UIKit.OutlinedFont(font);
            text.sortingOrder = 40;
            text.rectTransform.sizeDelta = new Vector2(6, 2);
            return text;
        }
    }
}
