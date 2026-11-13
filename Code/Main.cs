using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace PuniMod;

[ModInitializer(nameof(Initialize))]
public static class Main
{
    public const string Id = "PuniMod";
    public const string Res = "res://PuniMod";
    public static readonly MegaCrit.Sts2.Core.Logging.Logger Logger = new(Id, MegaCrit.Sts2.Core.Logging.LogType.Generic);
    public static void Initialize()
    {
        new Harmony(Id).PatchAll(Assembly.GetExecutingAssembly());
        Logger.Info("谱尼 · 七重封印已加载");
    }
}

public sealed class Puni : PlaceholderCharacterModel
{
    public override string PlaceholderID => "regent";
    public override Color NameColor => new("f5d17e");
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 99;
    public override int StartingGold => 99;
    public override IEnumerable<CardModel> StartingDeck => [
        ModelDb.Card<StrikePuni>(), ModelDb.Card<StrikePuni>(), ModelDb.Card<StrikePuni>(), ModelDb.Card<StrikePuni>(),
        ModelDb.Card<DefendPuni>(), ModelDb.Card<DefendPuni>(), ModelDb.Card<DefendPuni>(), ModelDb.Card<DefendPuni>(),
        ModelDb.Card<SacredLight>(), ModelDb.Card<SealRelease>()
    ];
    public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<SevenSealCore>()];
    public override CardPoolModel CardPool => ModelDb.CardPool<PuniCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<PuniRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<PuniPotionPool>();
    public override string CustomVisualPath => Main.Res + "/scenes/puni.tscn";
    public override string CustomRestSiteAnimPath => Main.Res + "/scenes/rest.tscn";
    public override string CustomMerchantAnimPath => Main.Res + "/scenes/merchant.tscn";
    public override string CustomCharacterSelectBg => Main.Res + "/scenes/select.tscn";
    public override string CustomIconTexturePath => Main.Res + "/images/icon.png";
    public override string CustomIconOutlineTexturePath => Main.Res + "/images/icon.png";
    public override string CustomCharacterSelectIconPath => Main.Res + "/images/select.png";
    public override string CustomCharacterSelectLockedIconPath => Main.Res + "/images/select.png";
    public override string CustomMapMarkerPath => Main.Res + "/images/map.png";
    public override string CustomIconPath => Main.Res + "/scenes/icon.tscn";
    public override Control CustomIcon => ResourceLoader.Load<PackedScene>(CustomIconPath).Instantiate<Control>();
    public override List<(string, string)> Localization => new CharacterLoc(
        "谱尼", "谱尼", "来自宇宙深处的圣灵王者。\n以圣灵之力解放七重封印，在轮回与永恒中展现真身。\n[gold]99 生命 · 99 金币 · 七重封印[/gold]",
        "祂", "祂", "祂的", "祂的", "永恒的圣光", "圣灵将指引前路。", "轮回终将到来。", "七重封印护住了谱尼的灵魂。", "金色的光芒回应着圣灵。",
        "谱尼卡牌", "添加谱尼的卡牌。");
}

public sealed class PuniCardPool : CustomCardPoolModel
{
    public override string Title => "Puni";
    public override string BigEnergyIconPath => Main.Res + "/images/energy.png";
    public override string TextEnergyIconPath => Main.Res + "/images/energy-text.png";
    public override float H => 0.13f;
    public override float S => 0.65f;
    public override float V => 1.12f;
    public override Color DeckEntryCardColor => new("f5d17e");
    public override bool IsColorless => false;
}

public sealed class PuniRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => new("f5d17e");
    public override string BigEnergyIconPath => Main.Res + "/images/energy.png";
    public override string TextEnergyIconPath => Main.Res + "/images/energy-text.png";
}
public sealed class PuniPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => new("f5d17e");
    public override string BigEnergyIconPath => Main.Res + "/images/energy.png";
    public override string TextEnergyIconPath => Main.Res + "/images/energy-text.png";
}

[Pool(typeof(PuniRelicPool))]
public abstract class PuniRelic : CustomRelicModel
{
    public override string PackedIconPath => Main.Res + "/images/relic.png";
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;
}

public class SevenSealCore : PuniRelic
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<UnboundSealCore>();
    public override List<(string,string)> Localization => new RelicLoc("七重封印", "战斗开始时获得 1 点圣灵。每打出一张谱尼卡牌，获得 1 点圣灵。每满 3 点，依次解放一道封印，最多七道。", "虚无、元素、能量、生命、轮回、永恒、圣洁。圣灵之名，终将重现。");
    public override async Task BeforeCombatStart() => await SealProgressPower.Advance(new BlockingPlayerChoiceContext(), Owner.Creature, 1, null);
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card.Owner == Owner && play.Card is PuniCard)
            await SealProgressPower.Advance(context, Owner.Creature, 1, play.Card);
    }
}

public sealed class UnboundSealCore : SevenSealCore
{
    private int _rewardedSeals;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override string PackedIconPath => Main.Res + "/images/relic-upgraded.png";
    public override RelicModel? GetUpgradeReplacement() => null;
    public override List<(string,string)> Localization => new RelicLoc("七重封印·神启", "替换七重封印。战斗开始时获得 3 点圣灵。每打出一张谱尼卡牌，获得 1 点圣灵。每解放一道封印，额外获得 6 点格挡并抽 1 张牌。每场战斗最多触发七次。", "每一道封印的松动，都激起新的圣光。");
    public override async Task BeforeCombatStart()
    { _rewardedSeals=0; await SealProgressPower.Advance(new BlockingPlayerChoiceContext(),Owner.Creature,3,null); }
    public async Task AfterSealUnlocked(PlayerChoiceContext context,int seal)
    {
        int flag=1<<(seal-1);
        if((_rewardedSeals&flag)!=0)return;
        _rewardedSeals|=flag;
        await MegaCrit.Sts2.Core.Commands.CreatureCmd.GainBlock(Owner.Creature,6,MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,null);
        await MegaCrit.Sts2.Core.Commands.CardPileCmd.Draw(context,1,Owner);
    }
}

public sealed class HolyHalo : PuniRelic
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override List<(string,string)> Localization => new RelicLoc("圣灵光环", "战斗开始时额外获得 3 点圣灵。", "第一道封印，只是苏醒的开始。");
    public override async Task BeforeCombatStart() => await SealProgressPower.Advance(new BlockingPlayerChoiceContext(), Owner.Creature, 3, null);
}

public sealed class EternalEssence : PuniRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override List<(string,string)> Localization => new RelicLoc("无尽能源", "每场战斗开始时获得 1 层轮回：抵挡一次死亡并回复最大生命值的 10%。", "生命可以终结，圣灵不会。");
    public override async Task BeforeCombatStart() => await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<RebirthPower>(new BlockingPlayerChoiceContext(), Owner.Creature, 1, Owner.Creature, null);
}



