using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TY.FXFacialAssistant
{
    internal enum BlacklistedShapeDisplay
    {
        ShowUnchecked,
        Hide
    }

    [Serializable]
    internal sealed class FacialBlacklistSettings
    {
        public BlacklistedShapeDisplay Display;
        public string Rules = FacialBlacklistDefaults.Rules;

        public string Signature => Hash128.Compute((int)Display + "\n" + (Rules ?? "")).ToString();
    }

    internal static class FacialBlacklistPreferences
    {
        private static string Key => "TY.FXFacialAssistant.Blacklist.v1." + Hash128.Compute(Application.dataPath);
        public static event Action Changed;

        public static FacialBlacklistSettings Load()
        {
            var settings = new FacialBlacklistSettings();
            if (EditorPrefs.HasKey(Key))
            {
                try { JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(Key), settings); }
                catch (ArgumentException) { return new FacialBlacklistSettings(); }
            }
            if (!Enum.IsDefined(typeof(BlacklistedShapeDisplay), settings.Display))
                settings.Display = BlacklistedShapeDisplay.ShowUnchecked;
            settings.Rules = settings.Rules ?? "";
            return settings;
        }

        public static void Save(FacialBlacklistSettings settings)
        {
            EditorPrefs.SetString(Key, JsonUtility.ToJson(settings));
            Changed?.Invoke();
        }
    }

    internal sealed class BlendShapeBlacklist
    {
        private readonly HashSet<string> exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> patterns = new List<string>();

        public int RuleCount => exact.Count + patterns.Count;

        public BlendShapeBlacklist(string rules)
        {
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in (rules ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var rule = line.Trim();
                if (rule.Length == 0 || rule.StartsWith("#", StringComparison.Ordinal) || !unique.Add(rule)) continue;
                if (rule.IndexOfAny(new[] { '*', '?' }) < 0) exact.Add(rule);
                else patterns.Add(rule);
            }
        }

        public bool Matches(string name)
            => name != null && (exact.Contains(name) || patterns.Any(pattern => MatchesPattern(name, pattern)));

        private static bool MatchesPattern(string name, string pattern)
        {
            var n = 0;
            var p = 0;
            var star = -1;
            var retry = 0;
            while (n < name.Length)
            {
                if (p < pattern.Length && pattern[p] != '*' && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(name[n])))
                { n++; p++; }
                else if (p < pattern.Length && pattern[p] == '*')
                { star = p++; retry = n; }
                else if (star >= 0)
                { p = star + 1; n = ++retry; }
                else return false;
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }
    }
}
