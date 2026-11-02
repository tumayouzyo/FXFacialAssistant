using System;
using UnityEditor;
using UnityEngine;

namespace TY.FXFacialAssistant
{
    internal static class FacialToolPreferences
    {
        private static readonly string Key = "TY.FXFacialAssistant.Tool." + Hash128.Compute(Application.dataPath);
        public static event Action Changed;
        public static bool English
        {
            get => EditorPrefs.GetBool(Key + ".English", false);
            set { EditorPrefs.SetBool(Key + ".English", value); Changed?.Invoke(); }
        }
        public static bool Prefix
        {
            get => EditorPrefs.GetBool(Key + ".Prefix", true);
            set { EditorPrefs.SetBool(Key + ".Prefix", value); Changed?.Invoke(); }
        }
    }
}
