using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TY.FXFacialAssistant
{
    internal static class FxControllerMaintenance
    {
        private static string EditablePath(AnimatorController controller)
        {
            var path = AssetDatabase.GetAssetPath(controller);
            if (!controller || !path.StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsOpenForEdit(controller))
                throw new InvalidOperationException("Assetsフォルダ内の編集可能なFX Controllerを指定してください。");
            return path;
        }

        internal static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
        {
            if (!machine) yield break;
            foreach (var child in machine.states) if (child.state) yield return child.state;
            foreach (var child in machine.stateMachines)
                foreach (var state in States(child.stateMachine)) yield return state;
        }

        public static int FillEmpty(AnimatorController controller, string layerName)
        {
            EditablePath(controller);
            var layer = FxControllerAccess.FindLayer(controller, layerName);
            if (layer == null || layer.syncedLayerIndex >= 0) throw new InvalidOperationException("編集対象のレイヤーが存在しないか、同期レイヤーに設定されています。");
            var candidates = AssetDatabase.FindAssets("proxy_empty t:AnimationClip", new[] { "Packages/com.vrchat.avatars" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p) == "proxy_empty.anim" && p.Contains("AV3 Demo Assets/")).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("SDKのAV3 Demo Assets内でproxy_emptyを一意に取得できませんでした。");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(candidates[0]);
            var states = States(layer.stateMachine).Where(s => !s.motion).ToArray();
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fill empty facial states");
            foreach (var state in states) FxControllerAccess.Assign(controller, state, clip);
            Undo.CollapseUndoOperations(group);
            return states.Length;
        }

        internal static string RemoveMissingLocalReferences(string yaml, out int count)
        {
            if (!yaml.StartsWith("%YAML", StringComparison.Ordinal)) throw new InvalidOperationException("テキスト形式のControllerのみ対応しています。");
            var ids = new HashSet<string>(Regex.Matches(yaml, @"(?m)^--- !u!\d+ &(-?\d+)")
                .Cast<Match>().Select(m => m.Groups[1].Value));
            var result = new StringBuilder(yaml.Length);
            var reference = new Regex(@"^([ \t]*(?:-[ \t]+)?(?:[A-Za-z_][A-Za-z0-9_]*:[ \t]*)?\{fileID:[ \t]*)(-?\d+)([ \t]*\}[ \t]*(?:#.*)?)(\r?\n)?$");
            var scalar = new Regex(@"^[ \t]*(?:-[ \t]+)?[A-Za-z_][A-Za-z0-9_]*:[ \t]*(.*)");
            var blockIndent = -1;
            var quote = '\0';
            count = 0;
            foreach (var line in Regex.Split(yaml, @"(?<=\n)"))
            {
                var indent = line.TakeWhile(char.IsWhiteSpace).Count();
                if (blockIndent >= 0 && (string.IsNullOrWhiteSpace(line) || indent > blockIndent))
                { result.Append(line); continue; }
                blockIndent = -1;
                if (quote != '\0')
                { ReadQuotedScalar(line, 0, ref quote); result.Append(line); continue; }
                var value = scalar.Match(line);
                if (value.Success)
                {
                    var text = value.Groups[1].Value;
                    if (Regex.IsMatch(text, @"^[>|][0-9+-]*\s*(?:#.*)?$")) blockIndent = indent;
                    if (text.StartsWith("\"") || text.StartsWith("'"))
                    { quote = text[0]; ReadQuotedScalar(text, 1, ref quote); result.Append(line); continue; }
                }
                var match = reference.Match(line);
                var id = match.Groups[2].Value;
                if (match.Success && id != "0" && !ids.Contains(id))
                {
                    var number = match.Groups[2];
                    result.Append(line.Substring(0, number.Index)).Append('0').Append(line.Substring(number.Index + number.Length));
                    count++;
                }
                else result.Append(line);
            }
            return result.ToString();
        }

        private static void ReadQuotedScalar(string text, int start, ref char quote)
        {
            for (var i = start; i < text.Length; i++)
            {
                if (quote == '"' && text[i] == '\\') { i++; continue; }
                if (text[i] != quote) continue;
                if (quote == '\'' && i + 1 < text.Length && text[i + 1] == '\'') { i++; continue; }
                quote = '\0';
                return;
            }
        }

        public static int Cleanup(AnimatorController controller)
        {
            var path = EditablePath(controller);
            if (AssetDatabase.LoadAllAssetsAtPath(path).Any(EditorUtility.IsDirty))
                throw new InvalidOperationException("FXに未保存の変更があります。先に保存してから実行してください。");
            var original = File.ReadAllText(path);
            var repaired = RemoveMissingLocalReferences(original, out var count);
            if (count == 0) return 0;
            var backup = "Library/TY_FXFacialAssistantBackup/" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(backup);
            File.Copy(path, backup + "/" + Path.GetFileName(path));
            File.Copy(path + ".meta", backup + "/" + Path.GetFileName(path) + ".meta");
            var staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(staged, repaired, new UTF8Encoding(false));
                File.Replace(staged, path, null);
            }
            finally { if (File.Exists(staged)) File.Delete(staged); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return count;
        }
    }
}
