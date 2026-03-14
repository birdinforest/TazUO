using System.Collections.Generic;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    internal struct DynamicDungeonLandOverride
    {
        public ushort TileId;
        public sbyte Z;

        public DynamicDungeonLandOverride(ushort tileId, sbyte z)
        {
            TileId = tileId;
            Z = z;
        }
    }

    internal sealed class DynamicDungeonLandOverrideManager
    {
        public static DynamicDungeonLandOverrideManager Instance { get; } = new DynamicDungeonLandOverrideManager();

        private readonly object _sync = new object();
        private readonly Dictionary<long, DynamicDungeonLandOverride> _overrides = new Dictionary<long, DynamicDungeonLandOverride>();
        private readonly HashSet<long> _dirtyChunks = new HashSet<long>();
        private readonly Queue<long> _dirtyChunkQueue = new Queue<long>();

        private bool _hasSession;
        private uint _activeEpoch;
        private int _sessionId;
        private int _mapId;
        private int _originX;
        private int _originY;
        private int _width;
        private int _height;

        private DynamicDungeonLandOverrideManager()
        {
        }

        /// <summary>
        /// Begin a session. Accepted only if packetEpoch >= _activeEpoch (new or same epoch).
        /// </summary>
        public bool BeginSession(uint packetEpoch, int sessionId, int mapId, int originX, int originY, int width, int height)
        {
            lock (_sync)
            {
                if (packetEpoch < _activeEpoch)
                    return false;

                _activeEpoch = packetEpoch;
                _hasSession = width > 0 && height > 0;
                _sessionId = sessionId;
                _mapId = mapId;
                _originX = originX;
                _originY = originY;
                _width = width;
                _height = height;
                _overrides.Clear();
                _dirtyChunks.Clear();
                _dirtyChunkQueue.Clear();
                return true;
            }
        }

        /// <summary>
        /// Clear session. Applied only when packetEpoch and sessionId match active state.
        /// </summary>
        public bool ClearSession(uint packetEpoch, int sessionId)
        {
            lock (_sync)
            {
                if (!_hasSession || packetEpoch != _activeEpoch || sessionId != _sessionId)
                    return false;

                _hasSession = false;
                _overrides.Clear();
                _dirtyChunks.Clear();
                _dirtyChunkQueue.Clear();
                return true;
            }
        }

        /// <summary>
        /// Clear all sessions. Applied when packetEpoch >= _activeEpoch; then _activeEpoch is set
        /// so older packets are rejected.
        /// </summary>
        public void ClearAll(uint packetEpoch)
        {
            lock (_sync)
            {
                if (packetEpoch < _activeEpoch)
                    return;

                _activeEpoch = packetEpoch;
                _hasSession = false;
                _sessionId = 0;
                _mapId = 0;
                _originX = 0;
                _originY = 0;
                _width = 0;
                _height = 0;
                _overrides.Clear();
                _dirtyChunks.Clear();
                _dirtyChunkQueue.Clear();
            }
        }

        /// <summary>
        /// Store a land override. Accepted only when packetEpoch == _activeEpoch and sessionId matches.
        /// </summary>
        public bool StoreOverride(uint packetEpoch, int sessionId, int x, int y, ushort tileId, sbyte z)
        {
            lock (_sync)
            {
                if (packetEpoch != _activeEpoch || sessionId != _sessionId || !IsWithinSessionBoundsNoLock(x, y))
                    return false;

                _overrides[ComposeKey(x, y)] = new DynamicDungeonLandOverride(tileId, z);
                return true;
            }
        }

        public bool IsSessionMap(int mapId)
        {
            lock (_sync)
            {
                return _hasSession && _mapId == mapId;
            }
        }

        public bool TryGetOverride(int mapId, int x, int y, out ushort tileId, out sbyte z)
        {
            lock (_sync)
            {
                tileId = 0;
                z = 0;

                if (!IsWithinActiveBoundsNoLock(mapId, x, y))
                    return false;

                DynamicDungeonLandOverride data;
                if (!_overrides.TryGetValue(ComposeKey(x, y), out data))
                    return false;

                tileId = data.TileId;
                z = data.Z;
                return true;
            }
        }

        public bool TryGetSessionBounds(out int mapId, out int originX, out int originY, out int width, out int height)
        {
            lock (_sync)
            {
                mapId = _mapId;
                originX = _originX;
                originY = _originY;
                width = _width;
                height = _height;
                return _hasSession;
            }
        }

        public void MarkDirtyChunkForWorldTile(int worldX, int worldY)
        {
            MarkDirtyChunk(worldX >> 3, worldY >> 3);
        }

        public void MarkDirtyChunk(int chunkX, int chunkY)
        {
            lock (_sync)
            {
                if (!_hasSession || chunkX < 0 || chunkY < 0)
                    return;

                long key = ComposeKey(chunkX, chunkY);
                if (_dirtyChunks.Add(key))
                    _dirtyChunkQueue.Enqueue(key);
            }
        }

        private int DrainDirtyChunks(int mapId, int maxChunks, List<long> output)
        {
            lock (_sync)
            {
                output.Clear();
                if (!_hasSession || _mapId != mapId || maxChunks <= 0)
                    return 0;

                int count = 0;
                while (count < maxChunks && _dirtyChunkQueue.Count > 0)
                {
                    long key = _dirtyChunkQueue.Dequeue();
                    _dirtyChunks.Remove(key);
                    output.Add(key);
                    count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Applies cached overrides to loaded chunks only. Chunks not loaded yet are handled
        /// by Chunk.Load read-through (TryGetOverride) when they are created later.
        /// Must be called from the main/update thread.
        /// </summary>
        public int RefreshDirtyLoadedChunks(ClassicUO.Game.Map.Map map, int maxChunksPerFrame)
        {
            if (map == null || maxChunksPerFrame <= 0)
                return 0;

            List<long> dirtyKeys = new List<long>(maxChunksPerFrame);
            int dirtyCount = DrainDirtyChunks(map.Index, maxChunksPerFrame, dirtyKeys);

            int patchedTiles = 0;
            for (int i = 0; i < dirtyCount; i++)
            {
                DecodeChunkKey(dirtyKeys[i], out int chunkX, out int chunkY);
                ClassicUO.Game.Map.Chunk chunk = map.GetChunk2(chunkX, chunkY, load: false);

                if (chunk == null || chunk.IsDestroyed)
                    continue;

                int worldBaseX = chunkX << 3;
                int worldBaseY = chunkY << 3;

                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        int wx = worldBaseX + x;
                        int wy = worldBaseY + y;

                        if (!TryGetOverride(map.Index, wx, wy, out ushort tileId, out sbyte z))
                            continue;

                        GameObject obj = chunk.GetHeadObject(x, y);
                        while (obj != null)
                        {
                            if (obj is Land land)
                            {
                                land.Graphic = tileId;
                                land.Z = z;
                                land.ApplyStretch(map, (ushort)wx, (ushort)wy, z);
                                land.UpdateScreenPosition();
                                patchedTiles++;
                                break;
                            }

                            obj = obj.TNext;
                        }
                    }
                }
            }

            return patchedTiles;
        }

        private bool IsWithinActiveBoundsNoLock(int mapId, int x, int y)
        {
            return _hasSession
                && _mapId == mapId
                && x >= _originX
                && y >= _originY
                && x < _originX + _width
                && y < _originY + _height;
        }

        private bool IsWithinSessionBoundsNoLock(int x, int y)
        {
            return _hasSession
                && x >= _originX
                && y >= _originY
                && x < _originX + _width
                && y < _originY + _height;
        }

        private static long ComposeKey(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }

        private static void DecodeChunkKey(long key, out int chunkX, out int chunkY)
        {
            chunkX = (int)(key >> 32);
            chunkY = (int)(key & 0xFFFFFFFF);
        }
    }
}
