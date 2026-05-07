using System;
using System.Collections.Generic;
using JmcModLib.Config;
using JmcModLib.Config.UI;
using JmcModLib.Utils;

namespace AllCardIs.Core
{
    /// <summary>
    /// JmcModLib-backed settings for AllCardIs.
    /// Changes made in the in-game settings UI are applied immediately.
    /// </summary>
    public static class AllCardIsSettings
    {
        private const string GeneralGroup = "基础设置";
        private const string DefaultTargetCard = "WHITE_NOISE";

        private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "白噪音", "CARD.WHITE_NOISE" },
            { "大奖", "CARD.JACKPOT" },
            { "添柴", "CARD.STOKE" },
            { "飞溅", "CARD.SPLASH" },
            { "狱火", "CARD.INFERNO" },
            { "吹哨", "CARD.WHISTLE" },
            { "打击", "CARD.STRIKE_DEFECT" },
            { "铸墙", "CARD.BULWARK" },
            { "冲刺", "CARD.DASH" },
            { "吊杀", "CARD.HANG" },
            { "爪击", "CARD.CLAW" },
            { "撕咬", "CARD.MAUL" },
            { "发现", "CARD.DISCOVERY" },
            { "拳斗", "CARD.FISTICUFFS" },
            { "暗影之盾", "CARD.SHADOW_SHIELD" },
            { "闪亮登场", "CARD.DRAMATIC_ENTRANCE" },
            { "华丽收场", "CARD.GRAND_FINALE" },
            { "蛇咬", "CARD.SNAKEBITE" },
            { "新生之喜", "CARD.BUNDLE_OF_JOY" },
            { "死亡收割", "CARD.REAPER" },
            { "完美打击", "CARD.PERFECTED_STRIKE" },
        };

        [UIToggle]
        [Config(
            "启用卡牌替换",
            group: GeneralGroup,
            Description = "关闭后不需要重启游戏；Harmony 补丁仍存在，但会直接读取这个字段并放行所有卡牌。",
            Key = "enabled",
            Order = 10)]
        public static bool Enabled = true;

        [UIInput(64)]
        [Config(
            "目标卡牌 ID",
            onChanged: nameof(OnTargetCardChanged),
            group: GeneralGroup,
            Description = "可输入 WHITE_NOISE 或 CARD.WHITE_NOISE；也支持内置中文别名，如：白噪音、死亡收割。修改后立即生效。",
            Key = "target_card",
            Order = 20)]
        public static string TargetCard = DefaultTargetCard;

        /// <summary>
        /// Normalized target id used by the patch, for example CARD.WHITE_NOISE.
        /// </summary>
        public static string TargetCardId => NormalizeCardId(TargetCard, fallbackToDefault: true);

        public static string NormalizeCardId(string? raw, bool fallbackToDefault = false)
        {
            string value = (raw ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                value = fallbackToDefault ? DefaultTargetCard : string.Empty;
            }

            if (value.Length == 0)
            {
                return string.Empty;
            }

            if (Aliases.TryGetValue(value, out string? mapped))
            {
                return mapped;
            }

            if (value.StartsWith("CARD.", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring("CARD.".Length);
            }

            return "CARD." + value.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// 这里只处理派生缓存失效。配置系统已经直接把新值写回 TargetCard 字段。
        /// </summary>
        private static void OnTargetCardChanged(string value)
        {
            CardReplacer.InvalidateTargetCache();
            ModLogger.Info($"AllCardIs 目标卡牌已改为：{NormalizeCardId(value, fallbackToDefault: true)}");
        }
    }
}
