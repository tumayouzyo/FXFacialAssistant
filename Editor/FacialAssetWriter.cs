using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TY.FXFacialAssistant
{
    internal sealed class FacialSavePaths
    {
        public string ClipPath { get; }
        public string IconPath { get; }
        public string ClipName => Path.GetFileNameWithoutExtension(ClipPath);

        public FacialSavePaths(string folder, string inputName)
        {
            FacialAssetWriter.ValidateFolder(folder);
            var name = NormalizeName(inputName);
            ClipPath = folder + "/FA_" + name + ".anim";
            IconPath = folder + "/FA_" + name + ".png";
        }

        internal static string NormalizeName(string input)
        {
            var name = (input ?? "").Trim();
            if (name.StartsWith("FA_", StringComparison.Ordinal)) name = name.Substring(3);
            if (string.IsNullOrWhiteSpace(name) || name.EndsWith(".", StringComparison.Ordinal)
                || name.IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0 || name.Any(char.IsControl))
                throw new ArgumentException("保存名を入力してください。ファイル名に使えない文字や末尾のピリオドは使用できません。");
            if (name.Length > 100) throw new ArgumentException("保存名は100文字以内で入力してください。");
            return name;
        }
    }

    internal static class FacialAssetWriter
    {
        public static void ValidateFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || (folder != "Assets" && !folder.StartsWith("Assets/", StringComparison.Ordinal))
                || folder.Contains('\\') || folder.Split('/').Any(p => string.IsNullOrWhiteSpace(p) || p == "." || p == ".."))
                throw new ArgumentException("保存先は、このプロジェクトのAssetsフォルダ内を指定してください。");
            foreach (var part in folder.Split('/'))
                if (part.IndexOfAny("<>:\"|?*".ToCharArray()) >= 0 || part.Any(char.IsControl) || part.EndsWith("."))
                    throw new ArgumentException("保存先フォルダ名に無効な文字が含まれています。");
        }

        public static string FromAbsoluteFolder(string absolute)
        {
            var root = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var selected = Path.GetFullPath(absolute).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(root, selected, StringComparison.OrdinalIgnoreCase)) return "Assets";
            if (!selected.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Animationとアイコンの保存先は、このプロジェクトのAssetsフォルダ内を選択してください。");
            var relative = "Assets/" + selected.Substring(root.Length + 1).Replace('\\', '/');
            ValidateFolder(relative);
            return relative;
        }

        public static bool HasExistingFiles(FacialSavePaths paths)
            => File.Exists(paths.ClipPath) || File.Exists(paths.IconPath);

        public static AnimationClip SaveAnimation(FacialSavePaths paths, AnimationClip clip)
        {
            if (!clip) throw new ArgumentNullException(nameof(clip));
            return Save(paths, clip, null);
        }

        public static void SaveIcon(FacialSavePaths paths, Texture2D icon)
        {
            if (!icon) throw new ArgumentNullException(nameof(icon));
            Save(paths, null, icon);
        }

        public static AnimationClip Save(FacialSavePaths paths, AnimationClip generated, Texture2D icon)
        {
            var writeClip = generated != null;
            var writeIcon = icon != null;
            if (!writeClip && !writeIcon) throw new ArgumentException("保存するAnimationまたはアイコンを指定してください。");
            var existing = writeClip ? AssetDatabase.LoadMainAssetAtPath(paths.ClipPath) : null;
            if ((writeClip && ((File.Exists(paths.ClipPath) && !(existing is AnimationClip)) || Directory.Exists(paths.ClipPath)))
                || (writeIcon && Directory.Exists(paths.IconPath)))
                throw new InvalidOperationException("保存先に別の種類のアセット、または同名フォルダがあります。");
            if (existing && !AssetDatabase.IsOpenForEdit(existing))
                throw new InvalidOperationException("既存のAnimationが読み取り専用です。");
            var png = writeIcon ? icon.EncodeToPNG() : null;
            if (writeIcon && (png == null || png.Length == 0)) throw new InvalidOperationException("PNGの生成に失敗しました。");
            var clip = existing as AnimationClip;
            var backupClip = clip ? Object.Instantiate(clip) : null;
            if (backupClip) backupClip.name = clip.name;
            var backupPng = writeIcon && File.Exists(paths.IconPath) ? File.ReadAllBytes(paths.IconPath) : null;
            var metaPath = paths.IconPath + ".meta";
            var backupMeta = writeIcon && File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : null;
            var clipWritten = false;
            var iconWritten = false;
            var stagedPng = Path.GetDirectoryName(paths.IconPath) + "/." + Path.GetFileName(paths.IconPath)
                + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                EnsureFolder(Path.GetDirectoryName(paths.ClipPath).Replace('\\', '/'));
                if (writeIcon) File.WriteAllBytes(stagedPng, png);
                if (writeClip)
                {
                    if (clip)
                    {
                        clipWritten = true;
                        EditorUtility.CopySerialized(generated, clip);
                        EditorUtility.SetDirty(clip);
                    }
                    else
                    {
                        clip = Object.Instantiate(generated);
                        clip.name = paths.ClipName;
                        AssetDatabase.CreateAsset(clip, paths.ClipPath);
                    }
                    clipWritten = true;
                    AssetDatabase.SaveAssetIfDirty(clip);
                }
                if (writeIcon)
                {
                    if (File.Exists(paths.IconPath)) File.Replace(stagedPng, paths.IconPath, null);
                    else File.Move(stagedPng, paths.IconPath);
                    iconWritten = true;
                    AssetDatabase.ImportAsset(paths.IconPath, ImportAssetOptions.ForceSynchronousImport);
                    var importer = AssetImporter.GetAtPath(paths.IconPath) as TextureImporter;
                    if (!importer) throw new InvalidOperationException("アイコンをTextureとして読み込めませんでした。");
                    ExpressionMenuIconImportSettings.Apply(importer);
                    importer.SaveAndReimport();
                }
                return clip;
            }
            catch
            {
                if (clipWritten)
                {
                    if (backupClip)
                    {
                        EditorUtility.CopySerialized(backupClip, clip);
                        EditorUtility.SetDirty(clip);
                        AssetDatabase.SaveAssetIfDirty(clip);
                    }
                    else AssetDatabase.DeleteAsset(paths.ClipPath);
                }
                if (iconWritten)
                {
                    if (backupPng != null)
                    {
                        File.WriteAllBytes(paths.IconPath, backupPng);
                        if (backupMeta != null) File.WriteAllBytes(metaPath, backupMeta);
                        AssetDatabase.ImportAsset(paths.IconPath, ImportAssetOptions.ForceSynchronousImport);
                    }
                    else AssetDatabase.DeleteAsset(paths.IconPath);
                }
                throw;
            }
            finally
            {
                if (backupClip) Object.DestroyImmediate(backupClip);
                if (File.Exists(stagedPng)) File.Delete(stagedPng);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder))))
                throw new IOException("保存先フォルダを作成できませんでした: " + folder);
        }
    }
}
