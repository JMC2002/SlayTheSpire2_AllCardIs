using AllCardIs.Patches;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using System.Linq;

namespace AllCardIs
{
    public static class CardReplacer
    {
        private static CardModel? _targetTemplate;

        public static bool ShouldReplace(CardModel card)
        {
            bool isAttack = card.Type == CardType.Attack;
            bool isCurse = card.Type == CardType.Curse;

            return (isAttack || isCurse) && card.Id.ToString() != ModConfig.TargetCardId;
        }

        public static CardModel? GetTarget()
        {
            if (_targetTemplate == null)
                _targetTemplate = ModelDb.AllCards.FirstOrDefault(c => c.Id.ToString() == ModConfig.TargetCardId);
            return _targetTemplate;
        }
    }
}