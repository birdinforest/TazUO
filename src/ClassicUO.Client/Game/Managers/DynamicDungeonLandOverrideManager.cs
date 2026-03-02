using System.Collections.Generic;

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

        private bool _hasSession;
        private int _sessionId;
        private int _mapId;
        private int _originX;
        private int _originY;
        private int _width;
        private int _height;

        private DynamicDungeonLandOverrideManager()
        {
        }

        public void BeginSession(int sessionId, int mapId, int originX, int originY, int width, int height)
        {
            lock (_sync)
            {
                _hasSession = width > 0 && height > 0;
                _sessionId = sessionId;
                _mapId = mapId;
                _originX = originX;
                _originY = originY;
                _width = width;
                _height = height;
                _overrides.Clear();
            }
        }

        public bool ClearSession(int sessionId)
        {
            lock (_sync)
            {
                if (!_hasSession || sessionId != _sessionId)
                    return false;

                _hasSession = false;
                _overrides.Clear();
                return true;
            }
        }

        public bool StoreOverride(int mapId, int x, int y, ushort tileId, sbyte z)
        {
            lock (_sync)
            {
                if (!IsWithinActiveBoundsNoLock(mapId, x, y))
                    return false;

                _overrides[ComposeKey(x, y)] = new DynamicDungeonLandOverride(tileId, z);
                return true;
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

        private bool IsWithinActiveBoundsNoLock(int mapId, int x, int y)
        {
            return _hasSession
                && _mapId == mapId
                && x >= _originX
                && y >= _originY
                && x < _originX + _width
                && y < _originY + _height;
        }

        private static long ComposeKey(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }
    }
}
