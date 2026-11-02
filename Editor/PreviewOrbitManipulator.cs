using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TY.FXFacialAssistant
{
    internal sealed class PreviewOrbitManipulator : PointerManipulator
    {
        private readonly Action<float> rotate;
        private readonly Action<float> zoom;
        private readonly Action<float> moveHeight;
        private Vector2 last;
        private int pointer = -1;
        private int button = -1;

        public PreviewOrbitManipulator(Action<float> rotate, Action<float> zoom, Action<float> moveHeight)
        {
            this.rotate = rotate;
            this.zoom = zoom;
            this.moveHeight = moveHeight;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerUpEvent>(OnUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            target.RegisterCallback<ContextClickEvent>(OnContextClick);
            target.RegisterCallback<WheelEvent>(OnWheel);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerUpEvent>(OnUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            target.UnregisterCallback<ContextClickEvent>(OnContextClick);
            target.UnregisterCallback<WheelEvent>(OnWheel);
        }

        private void OnDown(PointerDownEvent evt)
        {
            if ((evt.button != 1 && evt.button != 2) || pointer >= 0) return;
            pointer = evt.pointerId;
            button = evt.button;
            last = evt.position;
            target.CapturePointer(pointer);
            evt.StopPropagation();
        }

        private void OnMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != pointer || !target.HasPointerCapture(pointer)) return;
            var position = (Vector2)evt.position;
            if (button == 1) rotate((position.x - last.x) * 0.5f);
            else moveHeight(position.y - last.y);
            last = position;
            evt.StopPropagation();
        }

        private void OnUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointer || evt.button != button) return;
            target.ReleasePointer(pointer);
            pointer = -1;
            evt.StopPropagation();
        }

        private void OnCaptureOut(PointerCaptureOutEvent evt)
        {
            pointer = -1;
            button = -1;
        }
        private static void OnContextClick(ContextClickEvent evt) => evt.StopPropagation();

        private void OnWheel(WheelEvent evt)
        {
            zoom(evt.delta.y);
            evt.StopPropagation();
            evt.PreventDefault();
        }
    }
}
