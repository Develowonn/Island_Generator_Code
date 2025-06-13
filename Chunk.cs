// # System
using System;
using System.Collections.Generic;

// # Unity
using UnityEngine;

[System.Serializable]
public class Chunk
{
    public ChunkData        chunkData { get; private set; }

    // # 메쉬 오브젝트 관련
    private GameObject      chunkObject;
    private MeshRenderer    meshRenderer;
    private MeshFilter      meshFilter;
    private MeshCollider    meshCollider;

    // # 청크 크기 관련
    private int             chunkMaxWidth  = ChunkConfig.ChunkWidthValue - 1;
    private int             chunkMaxLength = ChunkConfig.ChunkLengthValue - 1;
    private int             chunkMaxHeight = ChunkConfig.ChunkHeightValue - 1;

    private Map map = null;

    public Chunk(Vector2Int coord, Map map, MapSettingManager mapSettingManager, ChunkType chunkType)
    {
        chunkData       = new ChunkData(coord, chunkType);
        this.map        = map;
        chunkObject     = mapSettingManager.InstantiateChunk();
        chunkObject.tag = "Ground";

        meshFilter      = chunkObject.GetComponent<MeshFilter>();
        meshRenderer    = chunkObject.GetComponent<MeshRenderer>();
        meshCollider    = chunkObject.GetComponent<MeshCollider>();

        chunkObject.transform.localPosition = new Vector3(coord.x * ChunkConfig.ChunkWidthValue, 0.0f, coord.y * ChunkConfig.ChunkLengthValue);
        chunkObject.name = $"Chunk {coord.x}.{coord.y}";

        switch (chunkType)
        {
            case ChunkType.Ground:
                chunkObject.transform.SetParent(mapSettingManager.GroundChunkParent);
                meshRenderer.material = mapSettingManager.MapGroundMaterial;
                break;

            case ChunkType.Water:
                chunkObject.transform.SetParent(mapSettingManager.WaterChunkParent);
                meshRenderer.material = mapSettingManager.MapWaterMaterial;
                break;
        }

        PopulateBlockHeight();
        PopulateChunkBlock();
        UpdateChunk();
    }

    ///<summary>현재 청크의 월드 공간 위치를 가져옵니다.</summary>
    public Vector3 Position
    {
        get { return chunkObject.transform.localPosition; }
    }

    public bool IsActive
    {
        get => chunkObject.activeSelf;
        set => chunkObject.SetActive(value);
    }

    private Vector3 ToWorldPos(in Vector3 pos) => Position + pos;
    private Vector3 ToWorldPos(int x, int y, int z) => Position + new Vector3(x, y, z);

    private void PopulateBlockHeight()
    {
        for (int x = 0; x < ChunkConfig.ChunkWidthValue; x++)
        {
            for (int z = 0; z < ChunkConfig.ChunkLengthValue; z++)
            {
                chunkData.blockHeights[x, z] = map.CalculateBlcokHeight(ToWorldPos(x, 0, z));
            }
        }
    }

    private void PopulateChunkBlock()
    {
        for (int y = 0; y < ChunkConfig.ChunkHeightValue; y++)
        {
            for (int x = 0; x < ChunkConfig.ChunkWidthValue; x++)
            {
                for (int z = 0; z < ChunkConfig.ChunkLengthValue; z++)
                {
                    chunkData.chunkBlocks[x, y, z] = map.CalculateBlockData(ToWorldPos(x, y, z), chunkData.blockHeights[x, z]);
                }
            }
        }
    }

    ///<summary>청크 데이터를 갱신하고 메시를 생성합니다.</summary>
    public void UpdateChunk()
    {
        MeshData meshData = ChunkMeshGenerator.Generate(this);
        ApplyMesh(meshData);
    }

    ///<summary>현재 청크의 메시를 생성하고 충돌 메시를 설정합니다.</summary>
    public void ApplyMesh(MeshData meshData)
    {
        Mesh mesh = new Mesh();
        mesh.SetVertices(meshData.Vertices);
        mesh.SetTriangles(meshData.Triangles, 0);
        mesh.SetUVs(0, meshData.UVs);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    ///<summary>지정된 위치의 복셀 데이터를 새로운 데이터로 변경합니다.</summary>
    public void EditVoxel(Vector3 pos, BlockData newBlockData)
    {
        Debug.Log($"블럭파괴! : {pos}, {newBlockData}");

        int globalX = Mathf.FloorToInt(pos.x);
        int globalY = Mathf.FloorToInt(pos.y);
        int globalZ = Mathf.FloorToInt(pos.z);

        int localX = globalX - Mathf.FloorToInt(Position.x);
        int localZ = globalZ - Mathf.FloorToInt(Position.z);

        chunkData.chunkBlocks[localX, globalY, localZ] = newBlockData;

        UpdateChunk();
        UpdateSurroundingVoxels(localX, globalY, localZ);
    }

    ///<summary>주변 청크의 메시를 갱신합니다.</summary>
    private void UpdateSurroundingVoxels(int x, int y, int z)
    {
        Vector3 thisVoxel = new Vector3(x, y, z);

        for (int p = 0; p < 6; p++)
        {
            Vector3 currentVoxel = thisVoxel + VoxelData.FaceChecks[p];

            if (!IsBlockInChunk((int)currentVoxel.x, (int)currentVoxel.y, (int)currentVoxel.z))
            {
                Vector3 pos = currentVoxel + Position;
                if (!map.IsVoxelInMap(pos)) return;

                Chunk tempChunk = map.GetChunkFromPosition(currentVoxel + Position, ChunkType.Ground);

                if(tempChunk != null)
                    tempChunk.UpdateChunk();
            }
        }
    }

    /// <summary> 블럭이 청크 내에 있는지 확인합니다. </summary>
    private bool IsBlockInChunk(int blockX, int blockY, int blockZ)
    {
        if (blockX < 0 || blockX > chunkMaxWidth
         || blockY < 0 || blockY > chunkMaxHeight
         || blockZ < 0 || blockZ > chunkMaxLength)
            return false;
        else
            return true;
    }

    private bool IsRenderBlock(BlockData blockData)
    {
        return chunkData.type switch
        {
            ChunkType.Ground => blockData.isSolid,
            ChunkType.Water => !blockData.isSolid,
            _ => false
        };
    }

    /// <summary>
    /// 지정된 위치의 복셀이 청크 내에 있는지 확인하고, 해당 복셀이 솔리드한지 검사합니다.
    /// </summary>
    public bool IsBlockSolid(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if (!IsBlockInChunk(x, y, z))
        {
            Vector3 globalPos = new Vector3(x, y, z) + Position;

            if (!map.IsVoxelInMap(globalPos))
                return false;

            int localX = Utils.PositiveMod(Mathf.FloorToInt(globalPos.x), ChunkConfig.ChunkWidthValue);
            int localY = Mathf.FloorToInt(globalPos.y);
            int localZ = Utils.PositiveMod(Mathf.FloorToInt(globalPos.z), ChunkConfig.ChunkLengthValue);

            Chunk chunk = map.GetChunkFromPosition(globalPos, chunkData.type);
            if (chunk == null) return false;

            return chunk.chunkData.chunkBlocks[localX, localY, localZ].isSolid;
        }

        return chunkData.chunkBlocks[x, y, z].id != BlockConstants.Air && IsRenderBlock(chunkData.chunkBlocks[x, y, z]);
    }
}
