using System.Linq;
using AllCardIs.Core;
using HarmonyLib;
using JmcModLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace AllCardIs.Patches
{
    [HarmonyPatch(typeof(RunState), "CreateCard", new[] { typeof(CardModel), typeof(Player) })]
    public static class RunState_CreateCard_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ref CardModel __0)
        {
            // 进阶之灾 / ASCENDERS_BANE 在游戏内部创建路径比较特殊，
            // 不能在 CreateCard Prefix 里直接把入参 CardModel 改成普通卡，
            // 否则会触发游戏内部的泛型/特殊构造逻辑崩溃。
            // 但它仍然会在 RunManager.Launch 的牌库清洗阶段参与替换。
            if (CardReplacer.ShouldBypassCreateCardPrefix(__0) || !CardReplacer.ShouldReplace(__0))
            {
                return;
            }

            CardModel? target = CardReplacer.GetTarget();
            if (target != null)
            {
                __0 = target;
            }
        }
    }

    [HarmonyPatch(typeof(RunManager), "Launch")]
    public static class RunManager_Launch_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(RunState __result)
        {
            if (__result == null || !AllCardIsSettings.Enabled)
            {
                return;
            }

            CardModel? target = CardReplacer.GetTarget();
            if (target == null)
            {
                return;
            }

            foreach (Player player in __result.Players)
            {
                CardPile deckPile = player.Deck;
                var toReplace = deckPile.Cards.Where(CardReplacer.ShouldReplace).ToList();

                foreach (CardModel card in toReplace)
                {
                    deckPile.RemoveInternal(card);
                    deckPile.AddInternal(__result.CreateCard(target, player));
                }

                if (toReplace.Count != 0)
                {
                    ModLogger.Info($"[Postfix] 牌库清洗：替换了 {toReplace.Count} 张卡牌为 {AllCardIsSettings.TargetCardId}");
                }
            }
        }
    }
}
