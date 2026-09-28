#if UNITY_EDITOR
using UIU.Simulator.Gameplay.NPC;
using UnityEditor;

namespace UIU.Simulator.Gameplay.Editor
{
    [CustomEditor(typeof(CampusWalkingNPC))]
    public sealed class CampusWalkingNPCEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            string message = ((CampusWalkingNPC)target).GetValidationMessage();
            if (message != null)
            {
                EditorGUILayout.HelpBox(message, MessageType.Warning);
            }

            DrawDefaultInspector();
        }
    }
}
#endif
