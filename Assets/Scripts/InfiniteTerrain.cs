using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteTerrain : MonoBehaviour
{
    private const float scale = 1;
    private const float _viewerMoveThresholdForChunkUpdate = 25f;
    private const float _sqrViewerMoveThresholdForChunkUpdate =
        _viewerMoveThresholdForChunkUpdate * _viewerMoveThresholdForChunkUpdate;

    public static float maxViewDistance;
    public static Vector2 viewerPosition;
    private static List<TerrainChunk> _terrainChunksVisibleLastUpdate;

    public Transform viewer;
    public Material mapMaterial;
    public LODInfo[] detailLevels;

    private int _chunkSize;
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
        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;

        _chunkSize = MapGenerator.mapChunkSize - 1;
        _chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance / _chunkSize);

        _terrainChunkDict = new Dictionary<Vector2, TerrainChunk>();
        _terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

        _mapGenerator = GameObject.FindObjectOfType<MapGenerator>();

        UpdateVisibleChunks();
    }

    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update()
    {
        viewerPosition = (new Vector2(viewer.position.x, viewer.position.z) / scale);

        if ((_prevViewerPosition - viewerPosition).sqrMagnitude > _sqrViewerMoveThresholdForChunkUpdate)
        {
            UpdateVisibleChunks();
            _prevViewerPosition = viewerPosition;
        }
    }

    private void UpdateVisibleChunks()
    {
        for (int i = 0; i < _terrainChunksVisibleLastUpdate.Count; i++)
            _terrainChunksVisibleLastUpdate[i].SetVisible(false);
        _terrainChunksVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / _chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / _chunkSize);

        for (int xOffset = -_chunksVisibleInViewDistance; xOffset <= _chunksVisibleInViewDistance;
                xOffset++)
        {
            for (int yOffset = -_chunksVisibleInViewDistance; yOffset <= _chunksVisibleInViewDistance;
                yOffset++)
            {
                Vector2 viewChunkCoord =
                    new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (_terrainChunkDict.ContainsKey(viewChunkCoord))
                {
                    _terrainChunkDict[viewChunkCoord].UpdateTerrainChunk();

                }
                else
                    _terrainChunkDict.Add(viewChunkCoord,
                        new TerrainChunk(viewChunkCoord, _chunkSize, detailLevels, transform, mapMaterial));
            }
        }
    }

    public class TerrainChunk
    {
        private GameObject _meshObject;
        private Vector2 _position;
        private Bounds _bounds;

        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;

        private LODInfo[] _detailLevels;
        private LODMesh[] _lodMeshes;

        private MapData _mapData;

        private bool _mapDataReceived;
        private int _prevLODIndex;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels,
            Transform parent, Material material)
        {
            _detailLevels = detailLevels;
            _prevLODIndex = -1;

            _position = coord * size;
            _bounds = new Bounds(_position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(_position.x, 0, _position.y);

            _meshObject = new GameObject("Terrain Chunk");
            _meshObject.transform.position = positionV3 * scale;
            _meshObject.transform.SetParent(parent);
            _meshObject.transform.localScale = Vector3.one * scale;

            _meshRenderer = _meshObject.AddComponent<MeshRenderer>();
            _meshRenderer.material = material;
            _meshFilter = _meshObject.AddComponent<MeshFilter>();

            // Dividing by 10 as plane is 10 units by default
            // _meshObject.transform.localScale = Vector3.one * size / 10f;
            SetVisible(false);

            _lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++)
                _lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);


            _mapGenerator.RequestMapData(_position, OnMapDataReceived);
        }

        public void UpdateTerrainChunk()
        {
            if (!_mapDataReceived)
                return;

            float viewerDistanceFromNearestEdge = Mathf.Sqrt(_bounds.SqrDistance(viewerPosition));
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

                _terrainChunksVisibleLastUpdate.Add(this);
            }

            SetVisible(visible);
        }

        public void SetVisible(bool visible) => _meshObject.SetActive(visible);

        public bool IsVisible() => _meshObject.activeSelf;

        private void OnMapDataReceived(MapData mapData)
        {
            _mapData = mapData;
            _mapDataReceived = true;

            Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap,
                MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            _meshRenderer.material.mainTexture = texture;

            UpdateTerrainChunk();
        }

        private void OnMeshDataReceived(MeshData meshData) => _meshFilter.mesh = meshData.CreateMesh();
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;

        private int _lod;
        private System.Action _updateCallback;

        public LODMesh(int lod, System.Action callback)
        {
            _lod = lod;
            _updateCallback = callback;
        }

        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            _mapGenerator.RequestMeshData(mapData, _lod, OnMeshDataReceived);
        }

        private void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            _updateCallback();
        }
    }

    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDistanceThreshold;
    }
}
