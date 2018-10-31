using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteTerrain : MonoBehaviour
{
    public static float maxViewDistance;
    public static Vector2 viewerPosition;

    public Transform viewer;
    public Material mapMaterial;
    public LODInfo[] detailLevels;

    private int _chunkSize;
    private int _chunksVisibleInViewDistance;

    private Dictionary<Vector2, TerrainChunk> _terrainChunkDict;
    private List<TerrainChunk> _terrainChunksVisibleLastUpdate;
    private static MapGenerator _mapGenerator;

    /// <summary>
    /// Start is called on the frame when a script is enabled just before
    /// any of the Update methods is called the first time.
    /// </summary>
    void Start()
    {
        _chunkSize = MapGenerator.mapChunkSize - 1;
        _chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance / _chunkSize);

        _terrainChunkDict = new Dictionary<Vector2, TerrainChunk>();
        _terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

        _mapGenerator = GameObject.FindObjectOfType<MapGenerator>();

        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;
    }

    /// <summary>
    /// Update is called every frame, if the MonoBehaviour is enabled.
    /// </summary>
    void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        UpdateVisibleChunks();
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
                    if (_terrainChunkDict[viewChunkCoord].IsVisible())
                        _terrainChunksVisibleLastUpdate.Add(_terrainChunkDict[viewChunkCoord]);
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
            _meshObject.transform.position = positionV3;
            _meshObject.transform.SetParent(parent);

            _meshRenderer = _meshObject.AddComponent<MeshRenderer>();
            _meshRenderer.material = material;
            _meshFilter = _meshObject.AddComponent<MeshFilter>();

            // Dividing by 10 as plane is 10 units by default
            // _meshObject.transform.localScale = Vector3.one * size / 10f;
            SetVisible(false);

            _lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < _lodMeshes.Length; i++)
                _lodMeshes[i] = new LODMesh(detailLevels[i].LOD);


            _mapGenerator.RequestMapData(OnMapDataReceived);
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
                        _meshFilter.mesh = lodMesh.mesh;
                        _prevLODIndex = lodIndex;
                    }
                    else if (!lodMesh.hasRequestedMesh)
                        lodMesh.RequestMesh(_mapData);
                }
            }

            SetVisible(visible);
        }

        public void SetVisible(bool visible) => _meshObject.SetActive(visible);

        public bool IsVisible() => _meshObject.activeSelf;

        private void OnMapDataReceived(MapData mapData)
        {
            _mapData = mapData;
            _mapDataReceived = true;
        }

        private void OnMeshDataReceived(MeshData meshData) => _meshFilter.mesh = meshData.CreateMesh();
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;

        private int _lod;

        public LODMesh(int lod)
        {
            this._lod = lod;
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
        }
    }

    [System.Serializable]
    public struct LODInfo
    {
        public int LOD;
        public float visibleDistanceThreshold;
    }
}
