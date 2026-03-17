using AllCardIs.Core;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using System;
using System.Linq;

namespace AllCardIs
{
    [HarmonyPatch(typeof(RunState), "CreateCard", new Type[] { typeof(CardModel), typeof(Player) })]
    public class RunState_CreateCard_Patch
    {
        private static CardModel _whiteNoiseTemplate = null;
        private static bool _initialized = false;

        [HarmonyPrefix]
        public static void Prefix(ref CardModel __0)
        {
            if (__0 == null) return;

            string originalId = __0.Id.ToString();

            if (!_initialized)
            {
                _initialized = true;
                ModLogger.Info("========================================");
                ModLogger.Info("开始在数据库中寻找白噪音模板...");

                // 精确提取 CARD.WHITE_NOISE
                _whiteNoiseTemplate = ModelDb.AllCards.FirstOrDefault(c => c.Id.ToString() == "CARD.WHITE_NOISE");

                if (_whiteNoiseTemplate != null)
                {
                    ModLogger.Info($"成功找到白噪音模板！");
                }
                else
                {
                    ModLogger.Error("致命错误：没有找到 CARD.WHITE_NOISE！");
                }
                ModLogger.Info("========================================");
            }

            if (_whiteNoiseTemplate != null)
            {
                // 过滤掉硬编码的系统卡牌，防止底层泛型强转时发生 InvalidCastException 崩溃
                if (originalId == "CARD.ASCENDERS_BANE")
                {
                    ModLogger.Warn($"跳过替换: 游戏尝试生成【{originalId}】，该卡牌底层使用了泛型强转，已放行。");
                    return;
                }

                // 替换非白噪音的其他所有卡牌
                if (originalId != "CARD.WHITE_NOISE")
                {
                    ModLogger.Info($"拦截发牌: 游戏尝试生成【{originalId}】，已被强行替换为【CARD.WHITE_NOISE】！");
                    __0 = _whiteNoiseTemplate; // 偷梁换柱！
                }
            }
        }
    }

    [HarmonyPatch(typeof(RunManager), "Launch")]
    public class RunManager_Launch_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(RunState __result)
        {
            if (__result == null) return;

            var whiteNoise = ModelDb.AllCards.FirstOrDefault(c => c.Id.ToString() == "CARD.WHITE_NOISE");
            if (whiteNoise == null) return;

            foreach (var player in __result.Players)
            {
                CardPile deckPile = player.Deck;

                var cardsToRemove = deckPile.Cards
                    .Where(c => c.Id.ToString() != "CARD.WHITE_NOISE")
                    .ToList();

                if (!cardsToRemove.Any()) continue;

                foreach (var card in cardsToRemove)
                {
                    deckPile.RemoveInternal(card);
                }

                for (int i = 0; i < cardsToRemove.Count; i++)
                {
                    var newCard = __result.CreateCard(whiteNoise, player);
                    deckPile.AddInternal(newCard);
                }

                ModLogger.Info($"牌库已清洗，已将 {cardsToRemove.Count} 张非白噪音牌替换。");
            }
        }
    }
}