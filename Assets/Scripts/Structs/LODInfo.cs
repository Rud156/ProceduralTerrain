using UnityEngine;

[System.Serializable]
public struct LODInfo
{
    [Range(0, MeshSettings.numSupportedLODs - 1)]
    public int lod;
    public float visibleDistanceThreshold;

    public float sqrVisibleDistanceThreshold
    {
        get
        {
            return visibleDistanceThreshold * visibleDistanceThreshold;
        }
    }
}
