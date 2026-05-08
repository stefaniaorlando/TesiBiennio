using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Holobiont
{
    /*
     * Detects left-clicks on creatures and routes the hit to a
     * CreatureTooltipView. One in the scene, owns the input + raycast.
     *
     * Creatures already carry a CircleCollider2D on the Nutrice / Scudo / Hub
     * prefabs, so Physics2D.OverlapPoint is enough — no per-creature wiring.
     *
     * Right-click or Esc dismisses the tooltip. A left-click that doesn't hit
     * any creature also dismisses (inspector-like dismiss behaviour).
     *
     * Update uses Time.deltaTime per project_conventions: clicks still register
     * while the simulation is paused (e.g. on the game-over screen).
     */
    [DisallowMultipleComponent]
    public class CreatureClickProbe : MonoBehaviour
    {
        // ----- Refs -----
        [Header("Refs")]
        [Tooltip("Camera used for ScreenToWorldPoint. Defaults to Camera.main when left empty.")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("Tooltip view to show/hide on hit.")]
        [SerializeField] private CreatureTooltipView tooltip;

        // ----- Filtering -----
        [Header("Filtering")]
        [Tooltip("Layers tested by Physics2D.OverlapPoint. Defaults to everything; restrict if your creatures live on a dedicated layer.")]
        [SerializeField] private LayerMask creatureLayers = ~0;

        // ----- UI raycast scratch (reused per frame, avoids per-click allocation) -----
        private static readonly List<RaycastResult> _uiHits = new();

        // ----- Lifecycle -----
        private void OnEnable()
        {
            if (!worldCamera) worldCamera = Camera.main;
            if (!tooltip)
            {
                Debug.LogError($"{nameof(CreatureClickProbe)} has no {nameof(CreatureTooltipView)} assigned.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            // Right-click or Esc dismisses regardless of where it landed.
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                tooltip.Hide();
                return;
            }

            if (!Input.GetMouseButtonDown(0)) return;

            // Block only when the pointer is over an *interactive* UI element
            // (Button, Toggle, etc.). Display-only Images and non-interactable
            // Sliders have raycastTarget=true but no Selectable or interactable=false,
            // so they must not eat creature clicks.
            if (IsPointerOverInteractableUI()) return;

            if (!worldCamera) worldCamera = Camera.main;
            if (!worldCamera) return;

            Vector3 mouseWorld = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 point = mouseWorld;

            Collider2D hit = Physics2D.OverlapPoint(point, creatureLayers);
            if (!hit) { tooltip.Hide(); return; }

            var creature = hit.GetComponentInParent<Creature>();
            if (creature) tooltip.Show(creature);
            else tooltip.Hide();
        }

        private static bool IsPointerOverInteractableUI()
        {
            if (!EventSystem.current) return false;
            var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            _uiHits.Clear();
            EventSystem.current.RaycastAll(data, _uiHits);
            foreach (var hit in _uiHits)
            {
                var sel = hit.gameObject.GetComponent<Selectable>();
                if (sel && sel.IsInteractable()) return true;
            }
            return false;
        }
    }
}
