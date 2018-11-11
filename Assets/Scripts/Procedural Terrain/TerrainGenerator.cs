using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Settings")]
    public MeshSettings meshSettings;
    public HeightMapSettings heightMapSettings;
    public TextureData textureData;

    [Header("Map Values")]
    public Transform viewer;
    public Material mapMaterial;
    public LODInfo[] detailLevels;
    public int colliderLODIndex;

    private const float _viewerMoveThresholdForChunkUpdate = 25f;
    private const float _sqrViewerMoveThresholdForChunkUpdate =
        _viewerMoveThresholdForChunkUpdate * _viewerMoveThresholdForChunkUpdate;

    private Vector2 _viewerPosition;
    private List<TerrainChunk> _visibleTerrainChunks;

    private float _meshWorldSize;
    private int _chunksVisibleInViewDistance;

    private Dictionary<Vector2, TerrainChunk> _terrainChunkDict;

    private Vector2 _prevViewerPosition;

    #region Singleton

    private static TerrainGenerator _instance;

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// </summary>
    void Awake()
    {
        if (_instance == null)
            _instance = this;

        if (_instance != this)
            Destroy(gameObject);
    }

    #endregion Singleton

    /// <summary>
    /// Start is called on the frame when a script is enabled just before
    /// any of the Update methods is called the first time.
    /// </summary>
    void Start()
    {
        _prevViewerPosition = new Vector2(int.MaxValue, int.MaxValue);

        textureData.ApplyToMaterial(mapMaterial);
        textureData.UpdateMeshHeights(mapMaterial,
            heightMapSettings.minHeight, heightMapSettings.maxHeight);

        _terrainChunkDict = new Dictionary<Vector2, TerrainChunk>();
        _visibleTerrainChunks = new List<TerrainChunk>();

        float maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;

        _meshWorldSize = meshSettings.meshWorldSize;
        _chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance / _meshWorldSize);

        UpdateVisibleChunks();
    }

    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update()
    {
        _viewerPosition =
            new Vector2(viewer.position.x, viewer.position.z);
        if (_viewerPosition != _prevViewerPosition)
            foreach (TerrainChunk chunk in _visibleTerrainChunks)
                chunk.UpdateCollisionMesh();


        if ((_prevViewerPosition - _viewerPosition).sqrMagnitude > _sqrViewerMoveThresholdForChunkUpdate)
        {
            UpdateVisibleChunks();
            _prevViewerPosition = _viewerPosition;
        }
    }

    private void UpdateVisibleChunks()
    {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();

        for (int i = _visibleTerrainChunks.Count - 1; i >= 0; i--)
        {
            alreadyUpdatedChunkCoords.Add(_visibleTerrainChunks[i].coord);
            _visibleTerrainChunks[i].UpdateTerrainChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(_viewerPosition.x / _meshWorldSize);
        int currentChunkCoordY = Mathf.RoundToInt(_viewerPosition.y / _meshWorldSize);

        for (int xOffset = -_chunksVisibleInViewDistance; xOffset <= _chunksVisibleInViewDistance;
                xOffset++)
        {
            for (int yOffset = -_chunksVisibleInViewDistance; yOffset <= _chunksVisibleInViewDistance;
                yOffset++)
            {
                Vector2 viewChunkCoord =
                    new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (!alreadyUpdatedChunkCoords.Contains(viewChunkCoord))
                {
                    if (_terrainChunkDict.ContainsKey(viewChunkCoord))
                    {
                        _terrainChunkDict[viewChunkCoord].UpdateTerrainChunk();
                    }
                    else
                    {
                        TerrainChunk newChunk = new TerrainChunk(viewChunkCoord,
                            heightMapSettings, meshSettings,
                            detailLevels, colliderLODIndex, transform, viewer, mapMaterial);
                        _terrainChunkDict.Add(viewChunkCoord, newChunk);
                        newChunk.onVisibilityChanged += OnTerrainChunkVisibilityChanged;
                        newChunk.Load();
                    }
                }
            }
        }
    }

    private void OnTerrainChunkVisibilityChanged(TerrainChunk terrainChunk, bool isVisible)
    {
        if (isVisible)
            _visibleTerrainChunks.Add(terrainChunk);
        else
            _visibleTerrainChunks.Remove(terrainChunk);
    }
}