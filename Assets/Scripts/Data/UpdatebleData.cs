using UnityEngine;

public class UpdatebleData : ScriptableObject
{
    public event System.Action OnValuesUpdated;
    public bool autoUpdate;

    /// <summary>
    /// Called when the script is loaded or a value is changed in the
    /// inspector (Called in the editor only).
    /// </summary>
    protected virtual void OnValidate()
    {
        if (autoUpdate)
            NotifyOnValuesUpdated();
    }

    public void NotifyOnValuesUpdated() => OnValuesUpdated?.Invoke();
}
