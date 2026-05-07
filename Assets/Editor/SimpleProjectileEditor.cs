using _Scripts.Creatures;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SimpleProjectile))]
public class SimpleProjectileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script", "arcHeight", "trajectoryType");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Trajectory", EditorStyles.boldLabel);

        var trajectoryProp = serializedObject.FindProperty("trajectoryType");
        EditorGUILayout.PropertyField(trajectoryProp);

        if ((TrajectoryType)trajectoryProp.enumValueIndex == TrajectoryType.Ballistic)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("arcHeight"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}

