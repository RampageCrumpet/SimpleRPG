using UnityEditor;
using UnityEngine;

// Menu entry points for ClothValidator. Nothing here changes a garment, collider, Cloth setting or animation; the component only reads.
public static class ClothValidatorMenu
{
    [MenuItem("Tools/Cloth Validator/Attach To Selected Cloth Character")]
    static void Attach()
    {
        var go = Selection.activeGameObject;
        if (!go) { Debug.LogWarning("Select the character (or the garment) first."); return; }
        var cloth = go.GetComponentInChildren<Cloth>(true) ?? go.GetComponentInParent<Cloth>();
        if (!cloth) { Debug.LogWarning("No Cloth under the selection."); return; }
        var owner = go.GetComponentInParent<Animator>() ? go.GetComponentInParent<Animator>().gameObject : go;
        var v = owner.GetComponent<ClothValidator>() ?? Undo.AddComponent<ClothValidator>(owner);
        v.cloth = cloth;
        Selection.activeObject = v;
        Debug.Log("ClothValidator attached to " + owner.name + ". Set startFrame/endFrame/detailFrames, enter Play Mode; the report is written to " + v.outDir + " (default ClothValidation/<scene>_<garment>) when the run ends.");
    }
}
