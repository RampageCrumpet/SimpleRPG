using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace SimpleRPG.EditorTools
{
    #if UNITY_EDITOR
    /// <summary>
    /// Fails a build when a roomset that ships in it has invalid rooms or rooms whose packed data is out of date,
    /// so a bad dungeon can never ship. Run the same check by hand with "Validate Roomset" on a roomset asset.
    /// </summary>
    public class RoomsetBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var problems = RoomsetTools.ValidateForBuild(RoomsetTools.FindShippedRoomsets());
            if (problems.Count == 0)
            {
                return;
            }

            throw new BuildFailedException(
                $"{problems.Count} roomset problem(s) found. Fix them, or re-pack the rooms with 'Package Rooms' on the roomset.\n" +
                string.Join("\n", problems.Take(20)) +
                (problems.Count > 20 ? $"\n...and {problems.Count - 20} more." : string.Empty));
        }
    }
    #endif
}
