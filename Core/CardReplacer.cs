using JmcModLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AllCardIs.Core
{
    public static class CardReplacer
    {
        private static readonly object SyncRoot = new();
        private static readonly object GenericCreatedMarker = new();
        private static readonly ConditionalWeakTable<CardModel, object> GenericCreatedCards = [];

        private static CardModel? targetTemplate;
        private static string? cachedTargetId;
        private static string? lastMissingTargetId;

        public static bool ShouldReplace(CardModel? card)
        {
            if (!AllCardIsSettings.Enabled || card == null)
            {
                return false;
            }

            string cardId = NormalizeExistingCardId(card.Id.ToString());
            return !string.Equals(cardId, AllCardIsSettings.TargetCardId, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldBypassCreateCardPrefix(CardModel? card)
        {
            return card == null;
        }

        public static bool IsCalledFromGenericRunStateCreateCard()
        {
            StackTrace stackTrace = new(skipFrames: 2, fNeedFileInfo: false);
            foreach (StackFrame frame in stackTrace.GetFrames() ?? Array.Empty<StackFrame>())
            {
                var method = frame.GetMethod();
                if (method == null)
                {
                    continue;
                }

                if (method.DeclaringType == typeof(RunState)
                    && method.Name == nameof(RunState.CreateCard)
                    && method.IsGenericMethod)
                {
                    return true;
                }
            }

            return false;
        }

        public static void MarkCreatedFromGeneric(bool wasGenericCreateCardCall, CardModel? card)
        {
            if (!wasGenericCreateCardCall
                || card == null
                || !ShouldReplace(card))
            {
                return;
            }

            GenericCreatedCards.Remove(card);
            GenericCreatedCards.Add(card, GenericCreatedMarker);
        }

        public static bool TryReplaceGenericCreatedCardForDeckAdd(IRunState runState, ref CardModel card, string source)
        {
            if (!AllCardIsSettings.Enabled || !GenericCreatedCards.TryGetValue(card, out _))
            {
                return false;
            }

            CardModel original = card;
            GenericCreatedCards.Remove(original);

            if (original.Owner == null)
            {
                ModLogger.Warn($"AllCardIs 检测到泛型创建卡牌 {original.Id} 准备入牌组，但 Owner 为空，跳过替换。来源：{source}");
                return false;
            }

            if (!ShouldReplace(original))
            {
                ModLogger.Debug($"AllCardIs 泛型创建卡牌已是目标牌，跳过入牌组替换：{original.Id}。来源：{source}");
                return false;
            }

            CardModel? target = GetTarget();
            if (target == null)
            {
                ModLogger.Warn($"AllCardIs 入牌组替换失败：找不到目标卡牌 {AllCardIsSettings.TargetCardId}。原卡：{original.Id}，来源：{source}");
                return false;
            }

            try
            {
                CardModel replacement = runState.CreateCard(target, original.Owner);
                replacement.FloorAddedToDeck = original.FloorAddedToDeck;
                CopyUpgradeLevel(original, replacement);

                // 原卡只用于事件内部强类型逻辑；真正入牌组前换成目标卡。
                original.RemoveFromState();
                card = replacement;
                ModLogger.Info($"AllCardIs 入牌组替换：{original.Id} -> {replacement.Id}，原升级={original.CurrentUpgradeLevel}，目标升级={replacement.CurrentUpgradeLevel}，来源={source}");
                return true;
            }
            catch (Exception exception)
            {
                ModLogger.Error($"AllCardIs 入牌组替换异常。原卡：{original.Id}，目标：{AllCardIsSettings.TargetCardId}，来源：{source}", exception);
                return false;
            }
        }

        public static CardModel? GetTarget()
        {
            if (!AllCardIsSettings.Enabled)
            {
                return null;
            }

            string targetId = AllCardIsSettings.TargetCardId;
            lock (SyncRoot)
            {
                if (targetTemplate != null
                    && string.Equals(cachedTargetId, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    return targetTemplate;
                }

                cachedTargetId = targetId;
                targetTemplate = ModelDb.AllCards.FirstOrDefault(card =>
                    string.Equals(NormalizeExistingCardId(card.Id.ToString()), targetId, StringComparison.OrdinalIgnoreCase));

                if (targetTemplate == null
                    && !string.Equals(lastMissingTargetId, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    lastMissingTargetId = targetId;
                    ModLogger.Warn($"AllCardIs 找不到目标卡牌：{targetId}。本次替换会被跳过，请检查配置项 target_card。 ");
                }

                return targetTemplate;
            }
        }

        public static void InvalidateTargetCache()
        {
            lock (SyncRoot)
            {
                targetTemplate = null;
                cachedTargetId = null;
                lastMissingTargetId = null;
            }
        }

        private static void CopyUpgradeLevel(CardModel source, CardModel target)
        {
            int upgradeCount = Math.Min(source.CurrentUpgradeLevel, target.MaxUpgradeLevel);
            for (int i = 0; i < upgradeCount && target.IsUpgradable; i++)
            {
                CardCmd.Upgrade(target, CardPreviewStyle.None);
            }
        }

        private static string NormalizeExistingCardId(string? cardId)
        {
            return AllCardIsSettings.NormalizeCardId(cardId, fallbackToDefault: false);
        }
    }
}
