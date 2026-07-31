using System.Collections.Generic;
using System.Linq;

namespace BOCCHI.Data;

/// <summary>
/// Curated North Horn content data used by the illegal-mode settings page.
/// IDs and locations are aligned with the in-game content rows.
/// </summary>
public static class NorthHornContent
{
    public readonly record struct ActivityInfo(
        uint Id,
        string JapaneseName,
        string EnglishName,
        string Location,
        bool IsMagicPot = false
    );

    public static readonly IReadOnlyList<ActivityInfo> CriticalEncounters =
    [
        new(49, "四つ顎の魔樹『ペレキュス』", "Many Mouths to Feed", "X:4.1 Y:10.3"),
        new(50, "魔女の複製体『カロフィステリ・ダブル』", "Doubled Trouble", "X:17.2 Y:20.2"),
        new(51, "白の守護者『アラバスターブレード』", "Quarried Away", "X:12.0 Y:8.0"),
        new(52, "禁忌の魔道書『アルバテル』", "Forbidden Folios", "X:34.7 Y:34.5"),
        new(53, "暗紅の屍竜『ルブルムドラゴン』", "Cursed Resurgence", "X:7.7 Y:24.5"),
        new(54, "大食の呪鬼『アルゴル』", "Imbalanced Diet", "X:36.7 Y:21.4"),
        new(55, "猟奇の母蜘蛛『クレセント・アルケニー』", "Web of Terror", "X:24.9 Y:18.8"),
        new(56, "反逆の使い魔『アトラス・カーバンクル』", "A Beast Unleashed", "X:26.1 Y:28.6"),
        new(57, "死霊使いの亡霊『マギ・ネクロマンサー』", "Dark Artistry", "X:25.9 Y:4.3"),
        new(58, "求道の人造人間『エルムギガース』", "Familiar Tactics", "X:13.5 Y:35.8"),
        new(59, "呪いを継ぐ者『ペイルマギア』", "Appalling Behavior", "X:38.2 Y:9.8"),
        new(60, "魔道兵団『タイニーメイジ』", "Tiny Terror", "X:24.3 Y:35.6"),
        new(61, "絶島の誘拐者『アブダクター』", "Lost on the Wind", "X:18.4 Y:4.3"),
        new(62, "覚醒の多頭竜『マギ・ヒュドラ』", "Ahead of the Competition", "X:19.8 Y:31.1"),
        new(63, "変化の使い魔『メタモルファ』", "Accept No Imitators", "X:31.4 Y:15.2"),
    ];

    public static readonly IReadOnlyList<ActivityInfo> Fates =
    [
        new(2072, "隠されのマジックポット（北）", "Daylight Pottery", "X:26.2 Y:11.6", true),
        new(2073, "飛ばされのマジックポット（南）", "In a Pot of Bother", "X:11.0 Y:25.8", true),
        new(2074, "暴力の牛魔『ミノタウロス・マキア』", "Raging Thrall", "X:35.8 Y:25.7"),
        new(2075, "呪いの宝珠『イビルシーア』", "Eye to Eye", "X:31.7 Y:20.8"),
        new(2076, "水辺の暴君『レグナントキマイラ』", "Shoreline Showdown", "X:23.3 Y:30.8"),
        new(2077, "歴戦水馬『アーチケルピー』", "Waved Away", "X:28.0 Y:16.4"),
        new(2078, "ため息モルボル『センシュアル・サンディ』", "Allure of the Occult", "X:13.4 Y:16.3"),
        new(2079, "自滅の歌い手『イアムベー』", "Inconstant Gardener", "X:18.0 Y:11.7"),
        new(2080, "遺跡荒らしの氷狼『ルーインハウンド』", "Territorial Dispute", "X:19.6 Y:38.7"),
        new(2081, "腐都の守護者『ペイシェント・クリブ』", "A Rotten Affair", "X:12.5 Y:5.4"),
        new(2082, "暴風の操者『ストームコーラー』", "Gale-force Encounter", "X:4.2 Y:31.0"),
        new(2083, "模造の蛇人形『デミメデューサ』", "Scale Model", "X:8.2 Y:20.1"),
        new(2084, "気高き雷獣『クレセントレギナ』", "Thunderregnum", "X:24.2 Y:7.3"),
    ];

    public static bool IsMagicPotFate(uint id)
    {
        return Fates.Any(fate => fate.Id == id && fate.IsMagicPot);
    }
}
