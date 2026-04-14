#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace HoloCade.Editor
{
    /// <summary>
    /// Deletes generated .meta files for KiCAD/non-Unity file types if they appear under the project,
    /// so KiCAD assets do not accumulate redundant metadata when present (e.g. in FirmwareExamples).
    /// </summary>
    public class KiCADMetaFileSuppressor : AssetPostprocessor
{
    /// <summary>
    /// Called before Unity processes an asset. Returns null to skip meta file generation.
    /// </summary>
    static void OnPreprocessAsset()
    {
        // This method is called for every asset, but we can't directly prevent
        // meta file generation here. Instead, we'll use OnPostprocessAllAssets
        // to delete meta files after they're created.
    }

    /// <summary>
    /// Called after Unity processes all assets. Deletes .meta files for KiCAD files.
    /// </summary>
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, 
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string assetPath in importedAssets)
        {
            // Check if this is a KiCAD file
            if (IsKiCADFile(assetPath))
            {
                string metaPath = assetPath + ".meta";
                if (System.IO.File.Exists(metaPath))
                {
                    System.IO.File.Delete(metaPath);
                    // Also delete the .meta.meta file if Unity created one
                    string metaMetaPath = metaPath + ".meta";
                    if (System.IO.File.Exists(metaMetaPath))
                    {
                        System.IO.File.Delete(metaMetaPath);
                    }
                }
            }
        }
    }

        /// <summary>
        /// Checks if a file path is a KiCAD project file.
        /// </summary>
        static bool IsKiCADFile(string assetPath)
        {
            string lowerPath = assetPath.ToLower();
            return lowerPath.EndsWith(".kicad_sch") ||
                   lowerPath.EndsWith(".kicad_pcb") ||
                   lowerPath.EndsWith(".kicad_pro") ||
                   lowerPath.EndsWith(".kicad_sym") ||
                   lowerPath.EndsWith(".kicad_mod") ||
                   lowerPath.EndsWith(".kicad_prl") ||
                   lowerPath.EndsWith(".sym") ||
                   lowerPath.Contains("pcbs/");
        }
    }
}
#endif

