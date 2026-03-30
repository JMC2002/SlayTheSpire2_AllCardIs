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
        public static readonly string TargetCardName = "蛇咬";
        public static readonly string TargetCardId; // 统一配置目标

        public static readonly Dictionary<string, string> Replacements = new()
        {
            { "白噪音", "CARD.WHITE_NOISE" },
            { "大奖", "CARD.JACKPOT" },
            { "打击", "CARD.STRIKE_DEFECT" },
            { "蛇咬", "CARD.SNAKEBITE" }
        };

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
        [HarmonyPrefix]
        public static void Prefix(ref CardModel __0)
        {
            if (__0.Id.ToString() == "CARD.ASCENDERS_BANE") return;

            if (CardReplacer.ShouldReplace(__0))
            {
                var target = CardReplacer.GetTarget();
                if (target != null)
                {
                    __0 = target;
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
            var target = CardReplacer.GetTarget();
            if (target == null) return;

            foreach (var player in __result.Players)
            {
                CardPile deckPile = player.Deck;
                var toReplace = deckPile.Cards.Where(CardReplacer.ShouldReplace).ToList();

                foreach (var card in toReplace)
                {
                    deckPile.RemoveInternal(card);
                    deckPile.AddInternal(__result.CreateCard(target, player));
                }
                if (toReplace.Count != 0) ModLogger.Info($"[Postfix] 牌库清洗: 替换了 {toReplace.Count} 张卡牌为 {ModConfig.TargetCardName}");
            }
        }
    }
}