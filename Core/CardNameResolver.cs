using JmcModLib.Utils;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace AllCardIs.Core
{
    public static class CardNameResolver
    {
        private const string CardIdPrefix = "CARD.";

        private static readonly object SyncRoot = new();
        private static Dictionary<string, IReadOnlyList<string>>? displayNameIndex;
        private static HashSet<string>? knownCardIds;
        private static string? indexLanguage;
        private static readonly HashSet<string> AmbiguousNamesLogged = new(StringComparer.OrdinalIgnoreCase);
        private static bool localeChangeSubscribed;
        private static bool modelDbUnavailableLogged;

        public static void EnsureLocaleChangeSubscription()
        {
            lock (SyncRoot)
            {
                if (localeChangeSubscribed || LocManager.Instance == null)
                {
                    return;
                }

                LocString.SubscribeToLocaleChange(OnLocaleChanged);
                localeChangeSubscribed = true;
            }
        }

        public static void InvalidateCache()
        {
            lock (SyncRoot)
            {
                displayNameIndex = null;
                knownCardIds = null;
                indexLanguage = null;
                AmbiguousNamesLogged.Clear();
            }
        }

        public static bool TryNormalizePrefixedCardId(string raw, out string normalizedCardId)
        {
            normalizedCardId = string.Empty;
            string value = raw.Trim();
            if (!value.StartsWith(CardIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string entry = value[CardIdPrefix.Length..].Trim();
            if (entry.Length == 0)
            {
                return false;
            }

            normalizedCardId = NormalizeCardIdEntry(entry);
            return true;
        }

        public static bool TryResolveExistingShortId(string raw, out string normalizedCardId)
        {
            normalizedCardId = string.Empty;
            string value = raw.Trim();
            if (!IsLikelyShortCardId(value))
            {
                return false;
            }

            string candidate = NormalizeCardIdEntry(value);
            try
            {
                if (!GetKnownCardIds().Contains(candidate))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                LogModelDbUnavailable(exception);
                return false;
            }

            normalizedCardId = candidate;
            return true;
        }

        public static bool TryResolveDisplayName(string raw, out string normalizedCardId)
        {
            normalizedCardId = string.Empty;
            string displayName = NormalizeDisplayName(raw);
            if (displayName.Length == 0)
            {
                return false;
            }

            Dictionary<string, IReadOnlyList<string>> index;
            try
            {
                index = GetDisplayNameIndex();
            }
            catch (Exception exception)
            {
                LogModelDbUnavailable(exception);
                return false;
            }

            if (!index.TryGetValue(displayName, out IReadOnlyList<string>? candidates))
            {
                return false;
            }

            if (candidates.Count == 1)
            {
                normalizedCardId = candidates[0];
                return true;
            }

            LogAmbiguousDisplayName(displayName, candidates);
            return false;
        }

        public static bool IsLikelyShortCardId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (char character in value.Trim())
            {
                bool isAsciiLetter = character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
                bool isAsciiDigit = character is >= '0' and <= '9';
                if (!isAsciiLetter && !isAsciiDigit && character != '_')
                {
                    return false;
                }
            }

            return true;
        }

        public static string NormalizeCardIdEntry(string entry)
        {
            return CardIdPrefix + entry.Trim().ToUpperInvariant();
        }

        private static Dictionary<string, IReadOnlyList<string>> GetDisplayNameIndex()
        {
            EnsureLocaleChangeSubscription();
            string currentLanguage = L10n.CurrentLanguage;
            lock (SyncRoot)
            {
                if (displayNameIndex != null
                    && string.Equals(indexLanguage, currentLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    return displayNameIndex;
                }

                displayNameIndex = BuildDisplayNameIndex();
                indexLanguage = currentLanguage;
                AmbiguousNamesLogged.Clear();
                ModLogger.Debug($"AllCardIs 已建立卡牌名称索引：语言={currentLanguage}，名称数={displayNameIndex.Count}。");
                return displayNameIndex;
            }
        }

        private static HashSet<string> GetKnownCardIds()
        {
            lock (SyncRoot)
            {
                if (knownCardIds != null)
                {
                    return knownCardIds;
                }

                knownCardIds = ModelDb.AllCards
                    .Select(card => NormalizeCardIdEntry(card.Id.Entry))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                return knownCardIds;
            }
        }

        private static Dictionary<string, IReadOnlyList<string>> BuildDisplayNameIndex()
        {
            var mutableIndex = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (CardModel card in ModelDb.AllCards)
            {
                string cardId = NormalizeCardIdEntry(card.Id.Entry);
                string displayName;
                try
                {
                    displayName = NormalizeDisplayName(card.TitleLocString.GetFormattedText());
                }
                catch (Exception exception)
                {
                    ModLogger.Warn($"AllCardIs 读取卡牌名称失败：{cardId}", exception);
                    continue;
                }

                if (displayName.Length == 0)
                {
                    continue;
                }

                if (!mutableIndex.TryGetValue(displayName, out List<string>? cardIds))
                {
                    cardIds = [];
                    mutableIndex[displayName] = cardIds;
                }

                if (!cardIds.Contains(cardId, StringComparer.OrdinalIgnoreCase))
                {
                    cardIds.Add(cardId);
                }
            }

            return mutableIndex.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.AsReadOnly(),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeDisplayName(string raw)
        {
            return raw.Trim();
        }

        private static void LogAmbiguousDisplayName(string displayName, IReadOnlyList<string> candidates)
        {
            lock (SyncRoot)
            {
                string logKey = $"{L10n.CurrentLanguage}:{displayName}";
                if (!AmbiguousNamesLogged.Add(logKey))
                {
                    return;
                }
            }

            string candidateText = string.Join("、", candidates);
            ModLogger.Warn($"AllCardIs 目标卡牌名称“{displayName}”不唯一，候选 ID：{candidateText}。请在 target_card 中改用明确 ID。");
        }

        private static void LogModelDbUnavailable(Exception exception)
        {
            lock (SyncRoot)
            {
                if (modelDbUnavailableLogged)
                {
                    return;
                }

                modelDbUnavailableLogged = true;
            }

            ModLogger.Debug($"AllCardIs 暂时无法读取卡牌数据库，目标卡牌会先按 ID 处理：{exception.Message}");
        }

        private static void OnLocaleChanged()
        {
            InvalidateCache();
            CardReplacer.InvalidateTargetCache();
            ModLogger.Info("AllCardIs 检测到语言变化，已清理卡牌名称解析缓存。");
        }
    }
}
