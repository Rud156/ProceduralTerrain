using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunk
{
    public event System.Action<TerrainChunk, bool> onVisibilityChanged;
    public Vector2 coord;

    private const float _colliderGenerationDistantThreshold = 5;

    private GameObject _meshObject;
    private Vector2 _sampleCenter;
    private Bounds _bounds;

    private MeshRenderer _meshRenderer;
    private MeshFilter _meshFilter;
    private MeshCollider _meshCollider;

    private LODInfo[] _detailLevels;
    private LODMesh[] _lodMeshes;
    private int _colliderLODIndex;

    private HeightMap _heightMap;

    private bool _heightMapReceived;
    private int _prevLODIndex;
    private bool _hasSetCollider;
    private float _maxViewDistance;

    private HeightMapSettings _heightMapSettings;
    private MeshSettings _meshSettings;

    private Transform _viewer;
    private Vector2 viewerPosition
    {
        get
        {
            return new Vector2(_viewer.position.x, _viewer.position.z);
        }
    }

    public TerrainChunk(Vector2 coord, HeightMapSettings heightMapSettings,
        MeshSettings meshSettings, LODInfo[] detailLevels,
        int colliderLODIndex, Transform parent, Transform viewer, Material material)
    {
        this.coord = coord;

        _detailLevels = detailLevels;
        _prevLODIndex = -1;
        _colliderLODIndex = colliderLODIndex;
        _heightMapSettings = heightMapSettings;
        _meshSettings = meshSettings;
        _viewer = viewer;

        _sampleCenter = coord * meshSettings.meshWorldSize / meshSettings.meshScale;
        Vector2 position = coord * meshSettings.meshWorldSize;
        _bounds = new Bounds(position, Vector2.one * meshSettings.meshWorldSize);

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

        _maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;
    }

    public void Load() =>
        ThreadedDataRequester.RequestData(
            () =>
                HeightMapGenerator.GenerateHeightMap(
                    _meshSettings.numVerticesPerLine,
                    _meshSettings.numVerticesPerLine,
                    _heightMapSettings,
                    _sampleCenter
                ),
            OnHeightMapReceived
        );

    public void UpdateTerrainChunk()
    {
        if (!_heightMapReceived)
            return;

        float viewerDistanceFromNearestEdge = Mathf.Sqrt(_bounds.SqrDistance(viewerPosition));
        bool wasVisible = IsVisible();
        bool visible = viewerDistanceFromNearestEdge <= _maxViewDistance;

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
                    lodMesh.RequestMesh(_heightMap, _meshSettings);
            }

        }
        if (wasVisible != visible)
        {
            SetVisible(visible);
            onVisibilityChanged?.Invoke(this, visible);
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
                _lodMeshes[_colliderLODIndex].RequestMesh(_heightMap, _meshSettings);
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

    private void OnHeightMapReceived(object heightMapObject)
    {
        _heightMap = (HeightMap)heightMapObject;
        _heightMapReceived = true;

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

    public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings)
    {
        hasRequestedMesh = true;
        ThreadedDataRequester.RequestData(
            () =>
                MeshGenerator.GenerateTerrainMesh(heightMap.values, _lod, meshSettings),
            OnMeshDataReceived
        );
    }

    private void OnMeshDataReceived(object meshDataObject)
    {
        mesh = ((MeshData)meshDataObject).CreateMesh();
        hasMesh = true;

        updateCallback?.Invoke();
    }
}