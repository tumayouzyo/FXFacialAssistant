using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TY.FXFacialAssistant
{
    internal sealed class FacialShapeRow : VisualElement
    {
        private readonly Toggle include = new Toggle();
        private readonly Label label = new Label();
        private readonly Slider slider = new Slider(0f, 100f);
        private readonly FloatField number = new FloatField();
        private readonly Toggle transition = new Toggle();
        private readonly Action previewChanged;
        private BlendShapeValue shape;

        public FacialShapeRow(Action previewChanged, Action selectionChanged)
        {
            this.previewChanged = previewChanged;
            AddToClassList("row");
            AddToClassList("shape-row");
            include.AddToClassList("shape-include");
            label.AddToClassList("shape-name");
            slider.AddToClassList("shape-slider");
            number.AddToClassList("shape-value");
            transition.AddToClassList("shape-transition");
            transition.tooltip = FacialLocalization.Text("デフォルト表情から現在の表情までを0～10f（60fps）で直線補間します。チェックを外すと固定値で記録します。");
            include.RegisterValueChangedCallback(evt => { if (shape != null) { shape.Include = evt.newValue; selectionChanged(); } });
            slider.RegisterValueChangedCallback(evt => SetValue(evt.newValue));
            number.RegisterValueChangedCallback(evt => SetValue(evt.newValue));
            transition.RegisterValueChangedCallback(evt => { if (shape != null) shape.Transition = evt.newValue; });
            Add(include);
            Add(label);
            Add(slider);
            Add(number);
            AddValueButton("最小値", 0f);
            AddValueButton("最大値", 100f);
            Add(transition);
        }

        private void AddValueButton(string caption, float value)
        {
            var button = new Button(() => SetValue(value)) { text = FacialLocalization.Text(caption) };
            button.AddToClassList("shape-button");
            Add(button);
        }

        public void Bind(BlendShapeValue value, bool blacklisted)
        {
            shape = value;
            include.SetValueWithoutNotify(value.Include);
            include.tooltip = FacialLocalization.Text(blacklisted
                ? "ブラックリストに一致しているため、初期状態ではチェックが外れています。手動でチェックするとアニメーションに記録します。" : "アニメーションに記録します。");
            label.text = value.Name;
            label.tooltip = FacialLocalization.Format("{0} / 指定時: {1}{2}", value.Name, value.DefaultValue,
                blacklisted ? FacialLocalization.Text(" / ブラックリストに一致") : "");
            slider.SetValueWithoutNotify(Mathf.Clamp(value.Value, 0f, 100f));
            number.SetValueWithoutNotify(value.Value);
            transition.SetValueWithoutNotify(value.Transition);
        }

        public void Unbind() => shape = null;

        private void SetValue(float value)
        {
            if (shape == null) return;
            if (float.IsNaN(value) || float.IsInfinity(value)) { number.SetValueWithoutNotify(shape.Value); return; }
            shape.Value = value;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, 0f, 100f));
            number.SetValueWithoutNotify(value);
            previewChanged();
        }
    }
}
