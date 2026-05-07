using System;
using System.Linq;
using JmcModLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace AllCardIs.Core
{
    public static class CardReplacer
    {
        private const string AscendersBane = "CARD.ASCENDERS_BANE";
        private static readonly object SyncRoot = new();

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

        /// <summary>
        /// Some cards are unsafe to replace by mutating RunState.CreateCard's CardModel argument directly.
        /// They can still be transformed later by deck cleanup in RunManager.Launch.
        /// </summary>
        public static bool ShouldBypassCreateCardPrefix(CardModel? card)
        {
            if (card == null)
            {
                return true;
            }

            string cardId = NormalizeExistingCardId(card.Id.ToString());
            return string.Equals(cardId, AscendersBane, StringComparison.OrdinalIgnoreCase);
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

        private static string NormalizeExistingCardId(string? cardId)
        {
            return AllCardIsSettings.NormalizeCardId(cardId, fallbackToDefault: false);
        }
    }
}
