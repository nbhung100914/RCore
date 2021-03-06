#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RCore.Service
{
    public class ServicesMenuTools : Editor
    {
        [MenuItem("RUtilities/Services/Add Firebase Manager")]
        private static void AddFirebaseManager()
        {
            var manager = FindObjectOfType<RFirebaseManager>();
            if (manager == null)
            {
                var obj = new GameObject("RFirebaseManager");
                obj.AddComponent<RFirebaseManager>();
            }
        }

        [MenuItem("RUtilities/Services/Add Game Services")]
        private static void AddGameServices()
        {
            var manager = FindObjectOfType<GameServices>();
            if (manager == null)
            {
                var obj = new GameObject("GameServices");
                obj.AddComponent<GameServices>();
            }
        }
    }
}
#endif