using AllCardIs.Core;
using HarmonyLib;
using JmcModLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace AllCardIs.Patches
{
    public readonly struct CreateCardPatchState
    {
        public CreateCardPatchState(bool wasGenericCreateCardCall, CardModel? sourceTemplate)
        {
            WasGenericCreateCardCall = wasGenericCreateCardCall;
            SourceTemplate = sourceTemplate;
        }

        public bool WasGenericCreateCardCall { get; }

        public CardModel? SourceTemplate { get; }
    }

    [HarmonyPatch(typeof(RunState), "CreateCard", new[] { typeof(CardModel), typeof(Player) })]
    public static class RunState_CreateCard_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ref CardModel __0, out CreateCardPatchState __state)
        {
            // CreateCard<T> 这类指定类型创建不能在这里提前改入参，
            // 否则会触发游戏内部的泛型强转崩溃。
            bool wasGenericCreateCardCall = CardReplacer.IsCalledFromGenericRunStateCreateCard();
            __state = new CreateCardPatchState(wasGenericCreateCardCall, null);
            if (wasGenericCreateCardCall)
            {
                return;
            }

            if (CardReplacer.ShouldBypassCreateCardPrefix(__0) || !CardReplacer.ShouldReplace(__0))
            {
                return;
            }

            CardModel? target = CardReplacer.GetTarget();
            if (target != null)
            {
                CardModel sourceTemplate = __0;
                __0 = target;
                __state = new CreateCardPatchState(false, sourceTemplate);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(CreateCardPatchState __state, CardModel? __result)
        {
            CardReplacer.MarkCreatedFromGeneric(__state.WasGenericCreateCardCall, __result);
            CardReplacer.MarkReplacementSource(__result, __state.SourceTemplate);
        }
    }

    [HarmonyPatch(typeof(RunState), nameof(RunState.CloneCard))]
    public static class RunState_CloneCard_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CardModel __0, CardModel? __result)
        {
            CardReplacer.TransferCardMetadata(__0, __result);
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardRewardUpgradeOdds))]
    public static class Hook_ModifyCardRewardUpgradeOdds_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(IRunState __0, CardModel __2, ref decimal __3)
        {
            CardReplacer.AdjustRewardUpgradeOdds(__0, __2, ref __3);
        }
    }

    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add), new[] { typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
    public static class CardPileCmd_AddSingleToPileType_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(ref CardModel __0, PileType __1)
        {
            if (__1 != PileType.Deck || __0.Owner == null)
            {
                return;
            }

            CardModel card = __0;
            if (CardReplacer.TryReplaceNewCardBeingAddedToDeck(ref card, "CardPileCmd.Add(CardModel, PileType.Deck)"))
            {
                __0 = card;
            }
        }
    }

    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add), new[] { typeof(CardModel), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
    public static class CardPileCmd_AddSingleToCardPile_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(ref CardModel __0, CardPile __1)
        {
            if (__1.Type != PileType.Deck || __0.Owner == null)
            {
                return;
            }

            CardModel card = __0;
            if (CardReplacer.TryReplaceNewCardBeingAddedToDeck(ref card, "CardPileCmd.Add(CardModel, CardPile.Deck)"))
            {
                __0 = card;
            }
        }
    }

    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add), new[] { typeof(IEnumerable<CardModel>), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
    public static class CardPileCmd_AddManyToPileType_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(ref IEnumerable<CardModel> __0, PileType __1)
        {
            if (__1 != PileType.Deck)
            {
                return;
            }

            CardReplacer.TryReplaceNewCardsBeingAddedToDeck(ref __0, "CardPileCmd.Add(IEnumerable<CardModel>, PileType.Deck)");
        }
    }

    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add), new[] { typeof(IEnumerable<CardModel>), typeof(CardPile), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
    public static class CardPileCmd_AddManyToCardPile_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(ref IEnumerable<CardModel> __0, CardPile __1)
        {
            if (__1.Type != PileType.Deck)
            {
                return;
            }

            CardReplacer.TryReplaceNewCardsBeingAddedToDeck(ref __0, "CardPileCmd.Add(IEnumerable<CardModel>, CardPile.Deck)");
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyCardBeingAddedToDeck))]
    public static class Hook_ModifyCardBeingAddedToDeck_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(IRunState __0, ref CardModel __1)
        {
            CardReplacer.TryReplaceCardBeingAddedToDeck(__0, ref __1, "Hook.ModifyCardBeingAddedToDeck");
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
                    CardModel replacement = __result.CreateCard(target, player);
                    CardReplacer.CopyReplacementState(card, replacement);
                    CardReplacer.MarkReplacementSource(replacement, card);
                    card.RemoveFromState();
                    deckPile.AddInternal(replacement);
                }

                if (toReplace.Count != 0)
                {
                    ModLogger.Info($"[Postfix] 牌库清洗：替换了 {toReplace.Count} 张卡牌为 {AllCardIsSettings.TargetCardId}");
                }
            }
        }
    }
}
