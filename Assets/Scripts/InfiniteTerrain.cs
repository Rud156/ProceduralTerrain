using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteTerrain : MonoBehaviour
{
    private const float _viewerMoveThresholdForChunkUpdate = 25f;
    private const float _sqrViewerMoveThresholdForChunkUpdate =
        _viewerMoveThresholdForChunkUpdate * _viewerMoveThresholdForChunkUpdate;
    private const float _colliderGenerationDistantThreshold = 5;

    public static float maxViewDistance;
    public static Vector2 viewerPosition;
    private static List<TerrainChunk> _visibleTerrainChunks;

    public Transform viewer;
    public Material mapMaterial;
    public LODInfo[] detailLevels;
    public int colliderLODIndex;

    private float _meshWorldSize;
    private int _chunksVisibleInViewDistance;

    private Dictionary<Vector2, TerrainChunk> _terrainChunkDict;
    private static MapGenerator _mapGenerator;

    private Vector2 _prevViewerPosition;

    /// <summary>
    /// Start is called on the frame when a script is enabled just before
    /// any of the Update methods is called the first time.
    /// </summary>
    void Start()
    {
        _terrainChunkDict = new Dictionary<Vector2, TerrainChunk>();
        _visibleTerrainChunks = new List<TerrainChunk>();

        _mapGenerator = GameObject.FindObjectOfType<MapGenerator>();

        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;

        _meshWorldSize = _mapGenerator.meshSettings.meshWordlSize;
        _chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance / _meshWorldSize);

        UpdateVisibleChunks();
    }

    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update()
    {
        viewerPosition =
            new Vector2(viewer.position.x, viewer.position.z);
        if (viewerPosition != _prevViewerPosition)
            foreach (TerrainChunk chunk in _visibleTerrainChunks)
                chunk.UpdateCollisionMesh();


        if ((_prevViewerPosition - viewerPosition).sqrMagnitude > _sqrViewerMoveThresholdForChunkUpdate)
        {
            UpdateVisibleChunks();
            _prevViewerPosition = viewerPosition;
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

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / _meshWorldSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / _meshWorldSize);

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
                        _terrainChunkDict.Add(viewChunkCoord,
                            new TerrainChunk(viewChunkCoord, _meshWorldSize,
                                detailLevels, colliderLODIndex, transform, mapMaterial));
                }
            }
        }
    }

    public class TerrainChunk
    {
        public Vector2 coord;

        private GameObject _meshObject;
        private Vector2 _sampleCenter;
        private Bounds _bounds;

        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private MeshCollider _meshCollider;

        private LODInfo[] _detailLevels;
        private LODMesh[] _lodMeshes;
        private int _colliderLODIndex;

        private HeightMap _mapData;

        private bool _mapDataReceived;
        private int _prevLODIndex;
        private bool _hasSetCollider;

        public TerrainChunk(Vector2 coord, float meshWorldSize, LODInfo[] detailLevels,
            int colliderLODIndex, Transform parent, Material material)
        {
            this.coord = coord;
            _detailLevels = detailLevels;
            _prevLODIndex = -1;
            _colliderLODIndex = colliderLODIndex;

            _sampleCenter = coord * meshWorldSize / _mapGenerator.meshSettings.meshScale;
            Vector2 position = coord * meshWorldSize;
            _bounds = new Bounds(position, Vector2.one * meshWorldSize);

            _meshObject = new GameObject("Terrain Chunk");
            _meshObject.transform.position = new Vector3(position.x, 0, position.y);
            _meshObject.transform.SetParent(parent);

            _meshRenderer = _meshObject.AddComponent<MeshRenderer>();
            _meshRenderer.material = material;
            _meshFilter = _meshObject.AddComponent<MeshFilter>();
            _meshCollider = _meshObject.AddComponent<MeshCollider>();

            // Dividing by 10 as plane is 10 units by default
            // _meshObject.transform.localScale = Vector3.one * size / 10f;
            SetVisible(false);

            _lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++)
            {
                _lodMeshes[i] = new LODMesh(detailLevels[i].lod);
                _lodMeshes[i].updateCallback += UpdateTerrainChunk;
                if (i == _colliderLODIndex)
                    _lodMeshes[i].updateCallback += UpdateCollisionMesh;
            }


            _mapGenerator.RequestHeightMap(_sampleCenter, OnMapDataReceived);
        }

        public void UpdateTerrainChunk()
        {
            if (!_mapDataReceived)
                return;

            float viewerDistanceFromNearestEdge = Mathf.Sqrt(_bounds.SqrDistance(viewerPosition));
            bool wasVisible = IsVisible();
            bool visible = viewerDistanceFromNearestEdge <= maxViewDistance;

            if (visible)
            {
                int lodIndex = 0;
                for (int i = 0; i < _detailLevels.Length - 1; i++)
                {
                    if (viewerDistanceFromNearestEdge > _detailLevels[i].visibleDistanceThreshold)
                        lodIndex = i + 1;
                    else
                        break;
                }

                if (lodIndex != _prevLODIndex)
                {
                    LODMesh lodMesh = _lodMeshes[lodIndex];
                    if (lodMesh.hasMesh)
                    {
                        _prevLODIndex = lodIndex;
                        _meshFilter.mesh = lodMesh.mesh;
                    }
                    else if (!lodMesh.hasRequestedMesh)
                        lodMesh.RequestMesh(_mapData);
                }

            }
            if (wasVisible != visible)
            {
                if (visible)
                    _visibleTerrainChunks.Add(this);
                else
                    _visibleTerrainChunks.Remove(this);
                SetVisible(visible);
            }
        }

        public void UpdateCollisionMesh()
        {
            if (_hasSetCollider)
                return;

            float sqrDistanceFromViewerToEdge = _bounds.SqrDistance(viewerPosition);

            if (sqrDistanceFromViewerToEdge < _detailLevels[_colliderLODIndex].sqrVisibleDistanceThreshold)
            {
                if (!_lodMeshes[_colliderLODIndex].hasRequestedMesh)
                    _lodMeshes[_colliderLODIndex].RequestMesh(_mapData);
            }

            if (sqrDistanceFromViewerToEdge <
                _colliderGenerationDistantThreshold * _colliderGenerationDistantThreshold)
            {
                if (_lodMeshes[_colliderLODIndex].hasMesh)
                {
                    _meshCollider.sharedMesh = _lodMeshes[_colliderLODIndex].mesh;
                    _hasSetCollider = true;
                }
            }
        }

        public void SetVisible(bool visible) => _meshObject.SetActive(visible);

        public bool IsVisible() => _meshObject.activeSelf;

        private void OnMapDataReceived(HeightMap mapData)
        {
            _mapData = mapData;
            _mapDataReceived = true;

            // Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap,
            //     MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            // _meshRenderer.material.mainTexture = texture;

            UpdateTerrainChunk();
        }

        private void OnMeshDataReceived(MeshData meshData) => _meshFilter.mesh = meshData.CreateMesh();
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        public event System.Action updateCallback;

        private int _lod;

        public LODMesh(int lod)
        {
            _lod = lod;
        }

        public void RequestMesh(HeightMap mapData)
        {
            hasRequestedMesh = true;
            _mapGenerator.RequestMeshData(mapData, _lod, OnMeshDataReceived);
        }

        private void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback?.Invoke();
        }
    }

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
}
