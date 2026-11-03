using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlanetDefender
{
    // Press: face sinks onto its lip and squashes; release: overshoot punch; hover: slight grow.
    public sealed class ButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform face;
        public float lipDepth;
        Button button;
        bool pressed;

        void Awake() => button = GetComponent<Button>();
        bool Live => button == null || button.interactable;

        public void OnPointerDown(PointerEventData e)
        {
            if (!Live) return;
            pressed = true;
            transform.DOKill(); face.DOKill();
            transform.DOScale(0.93f, 0.08f).SetUpdate(true);
            if (lipDepth > 0) face.DOAnchorPosY(-lipDepth * 0.7f, 0.06f).SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!pressed) return;
            pressed = false;
            transform.DOKill(); face.DOKill();
            face.DOAnchorPosY(0, 0.1f).SetUpdate(true);
            transform.DOScale(1f, 0.32f).SetEase(Ease.OutBack, 3f).SetUpdate(true);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!Live || pressed || e.pointerId >= 0) return;   // hover is a mouse-only state
            transform.DOKill();
            transform.DOScale(1.04f, 0.15f).SetUpdate(true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (pressed) return;
            transform.DOKill();
            transform.DOScale(1f, 0.15f).SetUpdate(true);
        }

        public void Attention()
        {
            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * 0.12f, 0.35f, 6, 0.6f).SetUpdate(true);
        }

        public void Deny()
        {
            transform.DOKill(true);
            transform.DOShakePosition(0.3f, new Vector3(10, 0, 0), 20, 0, false, true).SetUpdate(true);
        }

        void OnDisable() { pressed = false; transform.DOKill(); transform.localScale = Vector3.one; if (face) { face.DOKill(); face.anchoredPosition = Vector2.zero; } }
    }
}
