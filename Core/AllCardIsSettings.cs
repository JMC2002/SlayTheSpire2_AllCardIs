using JmcModLib.Config;
using JmcModLib.Config.UI;
using JmcModLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace AllCardIs.Core
{
    /// <summary>
    /// JmcModLib-backed settings for AllCardIs.
    /// Changes made in the in-game settings UI are applied immediately.
    /// </summary>
    public static class AllCardIsSettings
    {
        private const string GeneralGroup = "基础设置";
        private const string SourceTypeGroup = "源类型";
        private const string DefaultTargetCard = "WHITE_NOISE";

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
            "目标卡牌",
            onChanged: nameof(OnTargetCardChanged),
            group: GeneralGroup,
            Description = "可输入 WHITE_NOISE、CARD.WHITE_NOISE，或当前游戏语言下的卡牌名；重名卡牌请使用明确 ID。修改后立即生效。",
            Key = "target_card",
            Order = 20)]
        public static string TargetCard = DefaultTargetCard;

        [UIToggle]
        [Config(
            "替换攻击牌",
            group: SourceTypeGroup,
            Description = "勾选后，攻击牌会被替换为目标卡牌；取消后攻击牌保持原样。",
            Key = "source.attack",
            Order = 10)]
        public static bool ReplaceAttackCards = true;

        [UIToggle]
        [Config(
            "替换能力牌",
            group: SourceTypeGroup,
            Description = "勾选后，能力牌会被替换为目标卡牌；取消后能力牌保持原样。",
            Key = "source.power",
            Order = 20)]
        public static bool ReplacePowerCards = true;

        [UIToggle]
        [Config(
            "替换技能牌",
            group: SourceTypeGroup,
            Description = "勾选后，技能牌会被替换为目标卡牌；取消后技能牌保持原样。",
            Key = "source.skill",
            Order = 30)]
        public static bool ReplaceSkillCards = true;

        [UIToggle]
        [Config(
            "替换诅咒牌",
            group: SourceTypeGroup,
            Description = "勾选后，诅咒牌会被替换为目标卡牌；取消后诅咒牌保持原样。",
            Key = "source.curse",
            Order = 40)]
        public static bool ReplaceCurseCards = true;

        [UIToggle]
        [Config(
            "替换任务牌",
            group: SourceTypeGroup,
            Description = "勾选后，任务牌会被替换为目标卡牌；取消后任务牌保持原样。状态牌不会被此 MOD 替换。",
            Key = "source.quest",
            Order = 50)]
        public static bool ReplaceQuestCards = true;

        /// <summary>
        /// Normalized target id used by the patch, for example CARD.WHITE_NOISE.
        /// </summary>
        public static string TargetCardId => NormalizeCardId(TargetCard, fallbackToDefault: true);

        public static bool ShouldReplaceSourceType(CardType cardType)
        {
            return cardType switch
            {
                CardType.Attack => ReplaceAttackCards,
                CardType.Power => ReplacePowerCards,
                CardType.Skill => ReplaceSkillCards,
                CardType.Curse => ReplaceCurseCards,
                CardType.Quest => ReplaceQuestCards,
                _ => false,
            };
        }

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

            if (CardNameResolver.TryNormalizePrefixedCardId(value, out string normalizedCardId)
                || CardNameResolver.TryResolveExistingShortId(value, out normalizedCardId)
                || CardNameResolver.TryResolveDisplayName(value, out normalizedCardId))
            {
                return normalizedCardId;
            }

            return CardNameResolver.NormalizeCardIdEntry(value);
        }

        /// <summary>
        /// 这里只处理派生缓存失效。配置系统已经直接把新值写回 TargetCard 字段。
        /// </summary>
        private static void OnTargetCardChanged(string value)
        {
            CardNameResolver.InvalidateCache();
            CardReplacer.InvalidateTargetCache();
            ModLogger.Info($"AllCardIs 目标卡牌已改为：{NormalizeCardId(value, fallbackToDefault: true)}");
        }
    }
}
