using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UpdatebleData), true)]
public class UpdatableDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        UpdatebleData data = (UpdatebleData)target;

        if (GUILayout.Button("Update"))
        {
            data.NotifyOnValuesUpdated();
            EditorUtility.SetDirty(target);
        }
    }
}
