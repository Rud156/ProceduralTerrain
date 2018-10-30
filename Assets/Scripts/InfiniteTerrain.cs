using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfiniteTerrain : MonoBehaviour
{
    public const float maxViewDistance = 500;
    public static Vector2 viewerPosition;

    public Transform viewer;
    public Material mapMaterial;

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
                        new TerrainChunk(viewChunkCoord, _chunkSize, transform, mapMaterial));
            }
        }
    }

    public class TerrainChunk
    {
        private GameObject _meshObject;
        private Vector2 _position;
        private Bounds _bounds;

        private MapData _mapData;

        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;

        public TerrainChunk(Vector2 coord, int size, Transform parent, Material material)
        {
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

            _mapGenerator.RequestMapData(OnMapDataReceived);
        }

        public void UpdateTerrainChunk()
        {
            float viewerDistanceFromNearestEdge = Mathf.Sqrt(_bounds.SqrDistance(viewerPosition));
            bool visible = viewerDistanceFromNearestEdge <= maxViewDistance;
            SetVisible(visible);
        }

        public void SetVisible(bool visible) => _meshObject.SetActive(visible);

        public bool IsVisible() => _meshObject.activeSelf;

        private void OnMapDataReceived(MapData mapData) =>
            _mapGenerator.RequestMeshData(mapData, OnMeshDataReceived);

        private void OnMeshDataReceived(MeshData meshData) => _meshFilter.mesh = meshData.CreateMesh();
    }
}
