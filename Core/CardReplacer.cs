using JmcModLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
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
        private static readonly ConditionalWeakTable<CardModel, CardModel> ReplacementSources = [];

        private static CardModel? targetTemplate;
        private static string? cachedTargetId;
        private static string? lastMissingTargetId;

        public static bool ShouldReplace(CardModel? card)
        {
            if (!AllCardIsSettings.Enabled || card == null)
            {
                return false;
            }

            if (!AllCardIsSettings.ShouldReplaceSourceType(card.Type))
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
            if (!wasGenericCreateCardCall || card == null)
            {
                return;
            }

            GenericCreatedCards.Remove(card);
            GenericCreatedCards.Add(card, GenericCreatedMarker);
        }

        public static void MarkReplacementSource(CardModel? replacement, CardModel? sourceTemplate)
        {
            if (replacement == null || sourceTemplate == null)
            {
                return;
            }

            ReplacementSources.Remove(replacement);
            ReplacementSources.Add(replacement, sourceTemplate.CanonicalInstance);
        }

        public static void TransferCardMetadata(CardModel source, CardModel? target)
        {
            if (target == null)
            {
                return;
            }

            if (GenericCreatedCards.TryGetValue(source, out _))
            {
                GenericCreatedCards.Remove(target);
                GenericCreatedCards.Add(target, GenericCreatedMarker);
            }

            if (ReplacementSources.TryGetValue(source, out CardModel? sourceTemplate))
            {
                MarkReplacementSource(target, sourceTemplate);
            }
        }

        public static void AdjustRewardUpgradeOdds(IRunState runState, CardModel card, ref decimal originalOdds)
        {
            if (!ReplacementSources.TryGetValue(card, out CardModel? sourceTemplate))
            {
                return;
            }

            decimal sourceBonus = GetNaturalUpgradeActBonus(runState, sourceTemplate);
            decimal targetBonus = GetNaturalUpgradeActBonus(runState, card);
            decimal adjustment = sourceBonus - targetBonus;
            if (adjustment == 0m)
            {
                return;
            }

            originalOdds += adjustment;
            ModLogger.Debug($"AllCardIs 自然升级概率按原卡修正：{sourceTemplate.Id} -> {card.Id}，调整={adjustment:P0}，修正后={originalOdds:P0}");
        }

        public static bool TryReplaceNewCardBeingAddedToDeck(ref CardModel card, string source)
        {
            if (card.Owner == null || card.Pile != null || card.HasBeenRemovedFromState)
            {
                return false;
            }

            return TryReplaceCardBeingAddedToDeck(card.Owner.RunState, ref card, source);
        }

        public static bool TryReplaceNewCardsBeingAddedToDeck(ref IEnumerable<CardModel> cards, string source)
        {
            if (!AllCardIsSettings.Enabled)
            {
                return false;
            }

            List<CardModel> cardList = cards.ToList();
            bool replacedAny = false;
            for (int i = 0; i < cardList.Count; i++)
            {
                CardModel card = cardList[i];
                if (TryReplaceNewCardBeingAddedToDeck(ref card, $"{source}[{i}]"))
                {
                    cardList[i] = card;
                    replacedAny = true;
                }
            }

            cards = cardList;
            return replacedAny;
        }

        public static bool TryReplaceCardBeingAddedToDeck(IRunState runState, ref CardModel card, string source)
        {
            if (!AllCardIsSettings.Enabled || card.HasBeenRemovedFromState)
            {
                return false;
            }

            CardModel original = card;
            bool wasGenericCreated = GenericCreatedCards.TryGetValue(original, out _);
            GenericCreatedCards.Remove(original);

            if (original.Owner == null)
            {
                if (wasGenericCreated)
                {
                    ModLogger.Warn($"AllCardIs 检测到泛型创建卡牌 {original.Id} 准备入牌组，但 Owner 为空，跳过替换。来源：{source}");
                }

                return false;
            }

            if (!ShouldReplace(original))
            {
                if (wasGenericCreated)
                {
                    ModLogger.Debug($"AllCardIs 泛型创建卡牌无需入牌组替换：{original.Id}。来源：{source}");
                }

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
                CopyReplacementState(original, replacement);
                MarkReplacementSource(replacement, original);

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

        public static void CopyReplacementState(CardModel source, CardModel target)
        {
            target.FloorAddedToDeck = source.FloorAddedToDeck;
            CopyUpgradeLevel(source, target);
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

        public static void CopyUpgradeLevel(CardModel source, CardModel target)
        {
            int upgradeCount = Math.Min(source.CurrentUpgradeLevel, target.MaxUpgradeLevel);
            for (int i = 0; i < upgradeCount && target.IsUpgradable; i++)
            {
                CardCmd.Upgrade(target, CardPreviewStyle.None);
            }
        }

        private static decimal GetNaturalUpgradeActBonus(IRunState runState, CardModel card)
        {
            if (card.Rarity == CardRarity.Rare)
            {
                return 0m;
            }

            decimal upgradedCardOddScaling = AscensionHelper.GetValueIfAscension(AscensionLevel.Scarcity, 0.125m, 0.25m);
            return runState.CurrentActIndex * upgradedCardOddScaling;
        }

        private static string NormalizeExistingCardId(string? cardId)
        {
            return AllCardIsSettings.NormalizeCardId(cardId, fallbackToDefault: false);
        }
    }
}
