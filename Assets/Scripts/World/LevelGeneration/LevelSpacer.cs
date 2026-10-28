using UnityEngine;

namespace SimpleRPG.EditorOnly
{
    /// <summary>
    /// This class is used to visualize the level spacer in the editor.
    /// </summary>
    [ExecuteInEditMode]
    public class LevelSpacer : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 2.0f);
        }
    }
}
