using ToolQL.Protocol.Messages;

namespace ToolQL.Core;

/// <summary>Quái trong map: thông tin lúc vào map cộng HP hiện tại và còn sống hay không.</summary>
public sealed record MobState(Mob Info, long Hp, bool Alive);

/// <summary>Người chơi khác trong map. <see cref="Info"/> là thông tin lúc xuất hiện (hoặc lần đổi gần nhất).</summary>
public sealed record PlayerState(int Id, PlayerInfo Info, short X, short Y, long Hp, short BagId);

/// <summary>Nhân vật của mình. Tên ba loại tiền chưa kiểm chứng nên đặt trung tính (xem <see cref="MeStats"/>).</summary>
public sealed record MeState(
    int Id, string Name, int Race, int ClassIndex, short X, short Y, long Hp, long Mp,
    long MoneyA, int MoneyB, int MoneyC,
    ItemSlot?[] Equipment, ItemSlot?[] Bag, ItemSlot?[] Chest, short[] SkillIds);

/// <summary>Map hiện tại. <see cref="Tiles"/> null cho tới khi nhận lưới (<c>-28</c> sub 10).</summary>
public sealed record MapState(
    int Id, string Name, int TileSetId, int Width, int Height, byte[]? Tiles,
    Portal[] Portals, Npc[] Npcs, MapBackground[] Backgrounds);

/// <summary>
/// Ảnh chụp bất biến của trạng thái một acc: người nghe/bộ vẽ đọc thoải mái từ luồng khác mà không chạm vào luồng mạng.
/// Các mảng bên trong là bản sao riêng, không được sửa.
/// </summary>
public sealed record GameSnapshot(
    MeState? Me, MapState? Map,
    IReadOnlyList<MobState> Mobs, IReadOnlyList<DroppedItem> Items, IReadOnlyList<PlayerState> Players,
    IReadOnlyDictionary<int, int> UnhandledPackets, int ParseErrors, long Version);
