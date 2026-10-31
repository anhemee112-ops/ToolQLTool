using ToolQL.Protocol;
using ToolQL.Protocol.Messages;

namespace ToolQL.Core;

/// <summary>
/// Trạng thái game của một acc, dựng từ các gói S→C. Chỉ luồng đọc của phiên gọi <see cref="Apply"/>; luồng khác chỉ
/// lấy <see cref="Snapshot"/> (khóa ngắn). Acc ngủ không gọi <see cref="Snapshot"/> nên không tốn công sao chép.
/// Gói chưa có bộ đọc được đếm vào <c>UnhandledPackets</c>; gói đọc lỗi được đếm vào <c>ParseErrors</c>, không làm hỏng phiên.
/// </summary>
public sealed class GameState
{
    private readonly object _gate = new();
    private readonly Dictionary<int, int> _unhandled = new();
    private readonly Dictionary<int, PlayerState> _players = new();
    private readonly Dictionary<int, DroppedItem> _items = new();   // khóa: id vật rơi (DroppedItem.A)
    private MobState[] _mobs = Array.Empty<MobState>();
    private MeState? _me;
    private MapState? _map;
    private int _parseErrors;
    private long _version;

    /// <summary>Tăng sau mỗi gói làm đổi trạng thái; bộ vẽ so sánh để biết có cần vẽ lại không.</summary>
    public long Version { get { lock (_gate) return _version; } }

    /// <summary>Xóa sạch (khi nối lại: server sẽ gửi lại nhân vật và map).</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _players.Clear();
            _items.Clear();
            _unhandled.Clear();
            _mobs = Array.Empty<MobState>();
            _me = null;
            _map = null;
            _parseErrors = 0;
            _version++;
        }
    }

    /// <summary>Áp một gói S→C. Gói lạ hoặc đọc lỗi không ném ngoại lệ.</summary>
    public void Apply(Packet packet)
    {
        lock (_gate)
        {
            try
            {
                if (Handle(packet)) _version++;
                else Count(packet);
            }
            catch (InvalidDataException)
            {
                _parseErrors++;
            }
        }
    }

    public GameSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new GameSnapshot(
                _me is null ? null : _me with
                {
                    Equipment = (ItemSlot?[])_me.Equipment.Clone(),
                    Bag = (ItemSlot?[])_me.Bag.Clone(),
                    Chest = (ItemSlot?[])_me.Chest.Clone(),
                    SkillIds = (short[])_me.SkillIds.Clone(),
                },
                _map is null ? null : _map with { Tiles = (byte[]?)_map.Tiles?.Clone() },
                _mobs.ToArray(),
                _items.Values.ToArray(),
                _players.Values.ToArray(),
                new Dictionary<int, int>(_unhandled),
                _parseErrors,
                _version);
        }
    }

    private void Count(Packet packet)
    {
        int key = Key(packet);
        _unhandled[key] = _unhandled.GetValueOrDefault(key) + 1;
    }

    /// <summary>Khóa đếm gói chưa xử lý: mã lệnh; nhóm có byte sub (-28, -29, -30) thì <c>mã * 256 + sub</c>.</summary>
    public static int Key(Packet p) =>
        p.Command is -28 or -29 or -30 && p.Payload.Length > 0 ? p.Command * 256 + (sbyte)p.Payload[0] : p.Command;

    /// <summary>True nếu gói được xử lý và trạng thái có thể đã đổi.</summary>
    private bool Handle(Packet packet)
    {
        byte[] p = packet.Payload;
        switch (packet.Command)
        {
            case MapInfo.Command: OnMap(MapInfo.Parse(p)); return true;
            case -28 when p.Length > 0 && p[0] == MapGrid.Sub: OnGrid(MapGrid.Parse(p)); return true;
            case -30 when p.Length > 0: return OnSelfGroup(p);

            case PlayerAppear.Command: OnPlayerAppear(PlayerAppear.Parse(p)); return true;
            case PlayerMoved.Command:
            {
                var m = PlayerMoved.Parse(p);
                MovePlayer(m.Id, m.X, m.Y, null);
                return true;
            }
            case PlayerLeft.Command: _players.Remove(PlayerLeft.Parse(p).Id); return true;
            case PlayerPositions.Command:
                foreach (var pos in PlayerPositions.Parse(p).Players) MovePlayer(pos.Id, pos.X, pos.Y, pos.Hp);
                return true;
            case PlayerRelocated.Command:
            {
                var m = PlayerRelocated.Parse(p);
                MovePlayer(m.Id, m.X, m.Y, null);
                return true;
            }
            case MeRelocated.Command:
            {
                var m = MeRelocated.Parse(p);
                if (_me != null) _me = _me with { X = m.X, Y = m.Y };
                return true;
            }
            case PlayerBagChanged.Command:
            {
                var m = PlayerBagChanged.Parse(p);
                if (_players.TryGetValue(m.Id, out var pl)) _players[m.Id] = pl with { BagId = m.BagId };
                return true;
            }
            case PlayerOutfitChanged.Command:
            {
                var m = PlayerOutfitChanged.Parse(p);
                if (m is not { Head: { } head, Body: { } body, Leg: { } leg }) return false;   // dạng 5 byte: chưa rõ nghĩa, chỉ đếm
                if (_players.TryGetValue(m.Id, out var pl))
                    _players[m.Id] = pl with { Info = pl.Info with { Head = head, Body = body, Leg = leg } };
                return true;
            }

            case MobHit.Command:
            {
                var m = MobHit.Parse(p);
                SetMob(m.Index, s => s with { Hp = m.Hp });
                return true;
            }
            case MobDeath.Command: OnMobDeath(MobDeath.Parse(p)); return true;
            case MobRespawn.Command:
            {
                var m = MobRespawn.Parse(p);
                SetMob(m.Index, s => s with { Hp = m.Hp, Alive = true });
                return true;
            }
            case MobAttacksPlayer.Command:
            {
                // Client tính HP người đó = HP - sát thương (messages.md). Với MÌNH chờ server gửi HP (-30 sub 5).
                var m = MobAttacksPlayer.Parse(p);
                if (_players.TryGetValue(m.PlayerId, out var pl))
                    _players[m.PlayerId] = pl with { Hp = Math.Max(0, pl.Hp - m.Damage) };
                return true;
            }

            case ItemAppeared.Command:
            {
                var item = ItemAppeared.Parse(p).Item;
                _items.TryAdd(item.A, item);   // client bỏ qua nếu id đã có
                return true;
            }
            case ItemRemoved.Command: _items.Remove(ItemRemoved.Parse(p).Id); return true;
            case ItemPicked.Command: _items.Remove(ItemPicked.Parse(p).Id); return true;
        }
        return false;
    }

    /// <summary>Nhóm <c>-30</c>: sub 0 nhân vật, 4 chỉ số, 5/6 HP/MP mình, 7 đổi thông tin người khác, 9/13/14/15 HP.</summary>
    private bool OnSelfGroup(byte[] p)
    {
        switch ((sbyte)p[0])
        {
            case 0: OnCharacter(CharacterInfo.Parse(p)); return true;
            case 4:
            {
                var m = MeStats.Parse(p);
                if (_me != null) _me = _me with { Hp = m.Hp, Mp = m.Mp, MoneyA = m.MoneyA, MoneyB = m.MoneyB, MoneyC = m.MoneyC };
                return true;
            }
            case 5:
            {
                var m = MeHp.Parse(p);
                if (_me != null) _me = _me with { Hp = m.Hp };
                return true;
            }
            case 6:
            {
                var m = MeMp.Parse(p);
                if (_me != null) _me = _me with { Mp = m.Mp };
                return true;
            }
            case 7:
            {
                var m = PlayerInfoChanged.Parse(p);
                if (_players.TryGetValue(m.Id, out var pl))
                    _players[m.Id] = pl with { Info = m.Info, X = m.Info.X, Y = m.Info.Y, Hp = m.Info.Hp };
                return true;
            }
            case 8:
                _ = PlayerStatusByte.Parse(p);   // ý nghĩa chưa rõ: đọc để kiểm, không đổi trạng thái
                return false;
            case 9 or 13 or 14 or 15:
            {
                var m = PlayerHpUpdate.Parse(p);
                if (_me != null && m.Id == _me.Id) _me = _me with { Hp = m.Hp };
                else if (_players.TryGetValue(m.Id, out var pl)) _players[m.Id] = pl with { Hp = m.Hp };
                return true;
            }
        }
        return false;
    }

    private void OnCharacter(CharacterInfo c)
    {
        _me = new MeState(c.Id, c.Name, c.Race, c.ClassIndex, _me?.X ?? 0, _me?.Y ?? 0, _me?.Hp ?? 0, _me?.Mp ?? 0,
            c.LongAi, c.IntGf, c.IntCq, c.Equipment, c.Bag, c.Chest, c.SkillIds);
    }

    private void OnMap(MapInfo m)
    {
        var h = m.Header;
        var b = m.Body;
        _map = new MapState(h.MapId, h.Name, h.TileSetId, 0, 0, null, b.Portals, b.Npcs, b.Backgrounds);
        _mobs = b.Mobs.Select(x => new MobState(x, x.Hp, true)).ToArray();
        _items.Clear();
        foreach (var item in b.Items) _items[item.A] = item;
        _players.Clear();
        if (_me != null) _me = _me with { X = b.X, Y = b.Y };
    }

    private void OnGrid(MapGrid g)
    {
        if (_map != null) _map = _map with { Width = g.Width, Height = g.Height, Tiles = g.Tiles };
    }

    private void OnPlayerAppear(PlayerAppear a)
    {
        var i = a.Info;
        _players[a.Id] = new PlayerState(a.Id, i, i.X, i.Y, i.Hp, i.Bag);
    }

    private void MovePlayer(int id, short x, short y, long? hp)
    {
        if (_players.TryGetValue(id, out var pl)) _players[id] = pl with { X = x, Y = y, Hp = hp ?? pl.Hp };
    }

    private void OnMobDeath(MobDeath d)
    {
        SetMob(d.Index, s => s with { Hp = 0, Alive = false });
        foreach (var item in d.Drops) _items.TryAdd(item.A, item);
    }

    /// <summary>Chỉ số quái ngoài danh sách (gói tới trước map) thì bỏ qua.</summary>
    private void SetMob(int index, Func<MobState, MobState> change)
    {
        if (index >= 0 && index < _mobs.Length) _mobs[index] = change(_mobs[index]);
    }
}
