using UnityEngine;
using UnityEngine.EventSystems;

namespace RustyFishing
{
    public sealed class HoldControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public Vector2 Value {get;private set;}
        public bool Held {get;private set;}
        public RectTransform knob;      // optional moving knob (set at runtime); returns to centre on release
        public float knobRadius=90f;    // how far the knob may travel from the base centre (px)
        RectTransform rect;
        Vector2 origin; // dynamic-origin: neutral is re-seated at the touch point (demo parity)
        public void Awake()=>rect=(RectTransform)transform;
        public void OnPointerDown(PointerEventData e){Held=true;origin=Vector2.ClampMagnitude(LocalPoint(e),MaxOriginShift);Set(e);}
        public void OnDrag(PointerEventData e)=>Set(e);
        public void OnPointerUp(PointerEventData e){Held=false;Value=Vector2.zero;if(knob!=null)knob.anchoredPosition=Vector2.zero;}
        const float Deadzone=.15f, MaxOriginShift=40f;
        Vector2 LocalPoint(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var p);return p;}
        void Set(PointerEventData e){
            // Demo parity: measure from the dynamic origin (the touch point), not the dial centre —
            // so holding still = no movement, instead of drifting when you press off-centre.
            Vector2 p=LocalPoint(e)-origin;
            // Knob visual follows the finger (same offset the input uses), clamped inside the base.
            if(knob!=null)knob.anchoredPosition=Vector2.ClampMagnitude(p,knobRadius);
            // Unity UI local space has +y = UP, but the hook physics (ported from the demo)
            // expect +y = DOWN. Flip y so "push up" rises and "push down" sinks.
            Vector2 raw=new Vector2(p.x,-p.y)/(rect.rect.width*.42f);float mag=Mathf.Clamp01(raw.magnitude);
            // Rescale magnitude 0..1 across the post-deadzone travel (no hard jump at the edge).
            Value=mag<Deadzone?Vector2.zero:raw.normalized*((mag-Deadzone)/(1-Deadzone));}
    }
}
