using AllCardIs.Core;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace AllCardIs.Patches
{
    public static class ModConfig
    {
        // 这里存储 ID 的映射：原ID -> 目标ID
        // 如果想把所有牌改成“大奖”，可以保留这个字典结构，方便未来扩展
        public static readonly string TargetCardName = "大奖";
        public static readonly string TargetCardId; // 统一配置目标

        // 如果未来你想针对不同情况替换不同卡牌，可以在这里扩充字典
        public static readonly Dictionary<string, string> Replacements = new()
        {
            { "白噪音", "CARD.WHITE_NOISE" },
            { "大奖", "CARD.JACKPOT" },
            { "打击", "CARD.STRIKE_DEFECT" }
        };

        // 静态构造函数：在任何成员被访问前自动执行
        static ModConfig()
        {
            if (Replacements.TryGetValue(TargetCardName, out string? value))
            {
                TargetCardId = value;
            }
            else
            {
                // 默认回退值，防止找不到Key导致崩溃
                TargetCardId = "CARD.WHITE_NOISE";
            }
        }
    }

    [HarmonyPatch(typeof(RunState), "CreateCard", [typeof(CardModel), typeof(Player)])]
    public class RunState_CreateCard_Patch
    {
        private static CardModel _targetTemplate;

        [HarmonyPrefix]
        public static void Prefix(ref CardModel __0)
        {
            if (__0 == null) return;
            string originalId = __0.Id.ToString();

            // 1. 初始化模板 (懒加载)
            if (_targetTemplate == null)
            {
                _targetTemplate = ModelDb.AllCards.FirstOrDefault(static c => c.Id.ToString() == ModConfig.TargetCardId);
                if (_targetTemplate == null)
                    ModLogger.Error($"致命错误：未找到目标卡牌 {ModConfig.TargetCardName}");
            }

            // 2. 拦截替换
            if (_targetTemplate != null && originalId != ModConfig.TargetCardId)
            {
                // 放行名单
                if (originalId == "CARD.ASCENDERS_BANE") return;

                ModLogger.Info($"拦截发牌: {originalId} -> {ModConfig.TargetCardId}");
                __0 = _targetTemplate;
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
            var target = ModelDb.AllCards.FirstOrDefault(c => c.Id.ToString() == ModConfig.TargetCardId);
            if (target == null) return;

            foreach (var player in __result.Players)
            {
                CardPile deckPile = player.Deck;
                // 找出所有不是大奖的牌
                var toReplace = deckPile.Cards.Where(c => c.Id.ToString() != ModConfig.TargetCardId).ToList();

                foreach (var card in toReplace)
                {
                    deckPile.RemoveInternal(card);
                    deckPile.AddInternal(__result.CreateCard(target, player));
                }
                if (toReplace.Any()) ModLogger.Info($"牌库清洗: 替换了 {toReplace.Count} 张牌为 {ModConfig.TargetCardName}");
            }
        }
    }
}