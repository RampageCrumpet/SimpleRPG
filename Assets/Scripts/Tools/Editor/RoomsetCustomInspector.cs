using UnityEditor;
using UnityEngine;

namespace SimpleRPG.EditorTools
{
    #if UNITY_EDITOR
    /// <summary>
    /// Draws the roomset buttons. The work is done by <see cref="RoomsetTools"/>.
    /// </summary>
    [CustomEditor(typeof(Roomset))]
    public class RoomsetCustomInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            Roomset roomset = (Roomset)target;

            if (GUILayout.Button("Validate Roomset"))
            {
                RoomsetTools.ValidateAndLog(roomset);
            }

            if (GUILayout.Button("Package Rooms"))
            {
                RoomsetTools.PackAndLog(roomset);
            }
        }
    }
    #endif
}
