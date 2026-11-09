using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace PuniMod;

public record CardSpec(string Name, int Cost, CardType Type, CardRarity Rarity, TargetType Target, string Description,
    int Damage = 0, int DamageUp = 0, int Block = 0, int BlockUp = 0, int Magic = 0, int MagicUp = 0, int Extra = 0, int ExtraUp = 0,
    bool Exhaust = false, int CostUp = 0, string Rule = "", bool HasX = false, SealImprint[]? Imprints = null,
    bool Innate = false, bool InnateOnUpgrade = false, bool RemoveExhaustOnUpgrade = false, bool ReturnToDrawTop = false);

[Pool(typeof(PuniCardPool))]
public abstract partial class PuniCard(string key) : CustomCardModel(Catalog.Get(key).Cost, Catalog.Get(key).Type, Catalog.Get(key).Rarity, Catalog.Get(key).Target)
{
    protected abstract string Key { get; }
    public CardSpec Spec => Catalog.Get(Key);
    protected override bool HasEnergyCostX => Spec.HasX;
    public override List<(string,string)> Localization => new CardLoc(Spec.Name, Spec.Description);
    public override string CustomPortraitPath => Main.Res + "/images/cards/" + Key + ".png";
    public override string PortraitPath => CustomPortraitPath;
    public override string BetaPortraitPath => CustomPortraitPath;
    protected override HashSet<CardTag> CanonicalTags => Key switch {
        nameof(StrikePuni) => [CardTag.Strike], nameof(DefendPuni) => [CardTag.Defend], _ => []
    };
    public override IEnumerable<CardKeyword> CanonicalKeywords
    {
        get
        {
            if(Spec.Exhaust)yield return CardKeyword.Exhaust;
            if(Spec.Innate)yield return CardKeyword.Innate;
        }
    }
    public override bool GainsBlock => Spec.Block > 0;
    protected override CardLocation GetResultLocationForCardPlay()
    {
        var location=base.GetResultLocationForCardPlay();
        return Spec.ReturnToDrawTop&&location.pileType==PileType.Discard
            ?new CardLocation(Owner,PileType.Draw,CardPilePosition.Top):location;
    }
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            if(Spec.Rule=="Edict")yield return HoverTipFactory.FromPower<TemporaryEdictPower>();
            if(Spec.Imprints is {Length:>0})yield return HoverTipFactory.FromPower<ImprintGuidePower>();
            string text=Spec.Description+string.Join("",(Spec.Imprints??[]).Select(i=>i.Text));
            if(text.Contains("圣灵")||text.Contains("封印")||Spec.Imprints is {Length:>0})yield return HoverTipFactory.FromPower<SealProgressPower>();
            if(text.Contains("圣痕"))yield return HoverTipFactory.FromPower<RiftBurnPower>();
            if(text.Contains("轮回"))yield return HoverTipFactory.FromPower<RebirthPower>();
            if(Key==nameof(TrueManifestation))yield return HoverTipFactory.FromPower<ManifestedTrueFormPower>();
        }
    }
    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            yield return new DamageVar(Spec.Damage,ValueProp.Move);
            yield return new BlockVar(Spec.Block,ValueProp.Move);
            yield return new DynamicVar("Magic",Spec.Magic);
            yield return new DynamicVar("Extra",Spec.Extra);
            yield return new StringVar("XSpirit","X");
            var imprints=Spec.Imprints??[];
            for(int i=0;i<imprints.Length;i++) {
                var imprint=imprints[i];string name="ImprintValue"+i;
                yield return imprint.Effect switch {
                    "Attack" or "AllAttack"=>new DamageVar(name,imprint.Amount,ValueProp.Move),
                    "Block" or "BlockArtifact"=>new BlockVar(name,imprint.Amount,ValueProp.Move),
                    _=>new DynamicVar(name,imprint.Amount)
                };
            }
        }
    }
    protected override void OnUpgrade()
    {
        // UpgradeValueBy(0) still marks a value green in the native formatter.
        if(Spec.DamageUp!=0)DynamicVars.Damage.UpgradeValueBy(Spec.DamageUp);
        if(Spec.BlockUp!=0)DynamicVars.Block.UpgradeValueBy(Spec.BlockUp);
        if(Spec.MagicUp!=0)DynamicVars["Magic"].UpgradeValueBy(Spec.MagicUp);
        if(Spec.ExtraUp!=0)DynamicVars["Extra"].UpgradeValueBy(Spec.ExtraUp);
        var imprints=Spec.Imprints??[];
        for(int i=0;i<imprints.Length;i++)
            if(imprints[i].Upgrade!=0)DynamicVars["ImprintValue"+i].UpgradeValueBy(imprints[i].Upgrade);
        if(Spec.HasX)((StringVar)DynamicVars["XSpirit"]).StringValue="X+1";
        if (Spec.CostUp != 0) EnergyCost.UpgradeBy(Spec.CostUp);
        if (Spec.InnateOnUpgrade) AddKeyword(CardKeyword.Innate);
        if (Spec.RemoveExhaustOnUpgrade) RemoveKeyword(CardKeyword.Exhaust);
    }
    private decimal M => DynamicVars["Magic"].BaseValue;
    private decimal E => DynamicVars["Extra"].BaseValue;
    private Task Block(CardPlay play) => CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    private async Task Attack(PlayerChoiceContext ctx, CardPlay play, bool all = false, decimal? damage = null)
    {
        var command = DamageCmd.Attack(damage ?? DynamicVars.Damage.BaseValue).FromCard(this, play);
        if (all) command.TargetingAllOpponents(CombatState!);
        else { if (play.Target == null || play.Target.IsDead) return; command.Targeting(play.Target); }
        await command.Execute(ctx);
    }
    private Task Power<T>(PlayerChoiceContext ctx, decimal amount) where T : PowerModel, new()
        => PowerCmd.Apply<T>(ctx, Owner.Creature, amount, Owner.Creature, this);
    private Task Spirit(PlayerChoiceContext ctx, decimal amount) => SealProgressPower.Advance(ctx, Owner.Creature, (int)amount, this);
    private Task Draw(PlayerChoiceContext ctx, decimal amount) => CardPileCmd.Draw(ctx, amount, Owner);
    private async Task Fixed(PlayerChoiceContext ctx, Creature? target, decimal amount)
    {
        if (target != null && !target.IsDead)
            await CreatureCmd.Damage(ctx, target, amount, ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this, null);
    }
    private async Task PlayBase(PlayerChoiceContext ctx, CardPlay play)
    {
        if (Spec.Rule.Length > 0) { await PlayRule(ctx, play); return; }
        switch (Key)
        {
            case nameof(StrikePuni): case nameof(Aurora): case nameof(SpaceRupture):
                await Attack(ctx, play); break;
            case nameof(DefendPuni): await Block(play); break;
            case nameof(SacredLight): await Attack(ctx, play); await Draw(ctx, M); break;
            case nameof(SealRelease): case nameof(VoidVeil):
                await Block(play); await Spirit(ctx, M); break;
            case nameof(ElementalPulse): await Attack(ctx, play); await Fixed(ctx, play.Target, M); break;
            case nameof(SpiritGather): await Draw(ctx, M); break;
            case nameof(SoulInterference):
                await Block(play);
                if (play.Target is { IsDead: false } target)
                {
                    await PowerCmd.Apply<WeakPower>(ctx, target, M, Owner.Creature, this);
                    await PowerCmd.Apply<VulnerablePower>(ctx, target, M, Owner.Creature, this);
                }
                break;
            case nameof(SacredWard): await Block(play); await Power<ArtifactPower>(ctx, M); break;
            case nameof(LightWrath):
                int buffs = play.Target?.Powers.Count(p => p.TypeForCurrentAmount == PowerType.Buff) ?? 0;
                await Attack(ctx, play, damage: DynamicVars.Damage.BaseValue + buffs * M); break;
            case nameof(SealEcho): await Draw(ctx, M); await PlayerCmd.GainEnergy(E, Owner); break;
            case nameof(RadiantWave): await Attack(ctx, play, true); break;
            case nameof(VoidSeal): await Power<IntangiblePower>(ctx, M); break;
            case nameof(ElementSeal):
                foreach(var enemy in CombatState!.HittableEnemies.ToArray()) await Fixed(ctx, enemy, M);
                break;
            case nameof(EnergySeal): await Block(play); await Power<EnergyReflectionPower>(ctx, 1); break;
            case nameof(LifeSeal): await Power<LivingLightPower>(ctx, M); break;
            case nameof(Reincarnation):
                await Power<RebirthPower>(ctx, M); break;
            case nameof(Eternity):
                await PlayerCmd.GainEnergy(M, Owner); await Draw(ctx, E); break;
            case nameof(Purity):
                await SealProgressPower.Cleanse(Owner.Creature); await Power<ArtifactPower>(ctx, M); break;
            case nameof(WhirlingRift):
                await Attack(ctx, play, true);
                foreach(var enemy in CombatState!.HittableEnemies.ToArray())
                    await PowerCmd.Apply<RiftBurnPower>(ctx, enemy, M, Owner.Creature, this);
                break;
            case nameof(ThousandRays):
                await SealProgressPower.Cleanse(Owner.Creature); await Attack(ctx, play); break;
            case nameof(HolyLightQi): await Power<DoubleDamagePower>(ctx, M); break;
            case nameof(HolySpiritFlash):
                decimal before = play.Target?.CurrentHp ?? 0;
                await Attack(ctx, play); await Fixed(ctx, play.Target, Math.Ceiling(before / 8m)); break;
            case nameof(SevenSeals): await Spirit(ctx, M); break;
            case nameof(TrueManifestation): await Power<ManifestedTrueFormPower>(ctx, 1); await Block(play); break;
            case nameof(BrilliantHolyLight):
                await Power<IntangiblePower>(ctx, E);
                await CreatureCmd.Heal(Owner.Creature, Math.Ceiling(Owner.Creature.MaxHp/3m));
                await Power<DoubleDamagePower>(ctx, 2); break;
            case nameof(SoulRequiem):
                await Attack(ctx, play, true);
                foreach(var enemy in CombatState!.HittableEnemies.ToArray())
                {
                    await PowerCmd.Apply<WeakPower>(ctx, enemy, M, Owner.Creature, this);
                    await PowerCmd.Apply<VulnerablePower>(ctx, enemy, M, Owner.Creature, this);
                }
                break;
            case nameof(HolyShadowBreak):
                await SealProgressPower.Cleanse(Owner.Creature);
                await Attack(ctx, play); await CreatureCmd.Heal(Owner.Creature, M);
                await Power<StrengthPower>(ctx, E); break;
            case nameof(EternalCycle): await Power<EternalPower>(ctx, M); break;
            case nameof(SealResonance):
                await Block(play);
                await Draw(ctx, SealProgressPower.Unlocked(Owner.Creature)); break;
            case nameof(HolyOrigin):
                await Attack(ctx, play, true);
                foreach (var enemy in CombatState!.HittableEnemies.ToArray()) await Fixed(ctx, enemy, M);
                await Draw(ctx,E);
                break;
            case nameof(BookOfEternity): await Spirit(ctx, 21); await Draw(ctx, M); await PlayerCmd.GainEnergy(E,Owner); break;
            default: throw new InvalidOperationException("Unimplemented card: " + Key);
        }
    }

    private async Task Debuff<T>(PlayerChoiceContext ctx, Creature? target, decimal amount) where T : PowerModel, new()
    {
        if(target is { IsDead:false }) await PowerCmd.Apply<T>(ctx,target,amount,Owner.Creature,this);
    }
    private Task BlockAmount(CardPlay play, decimal amount)
        => CreatureCmd.GainBlock(Owner.Creature,amount,ValueProp.Move,play);
    private async Task PlayRule(PlayerChoiceContext ctx, CardPlay play)
    {
        var target=play.Target;
        var seals=SealProgressPower.Unlocked(Owner.Creature);
        switch(Spec.Rule)
        {
            case "PurifyingExhaust":
                await Block(play);await ExhaustChosen(ctx,PileType.Hand,(int)M);break;
            case "QuietExhaust":
                await SealProgressPower.Cleanse(Owner.Creature);await ExhaustChosen(ctx,PileType.Hand,(int)M);break;
            case "BurnAttack":
                int burned=await ExhaustChosen(ctx,PileType.Hand,1);
                await Attack(ctx,play,damage:DynamicVars.Damage.BaseValue+(burned>0?M:0));break;
            case "BurnStrength":
                if(await ExhaustChosen(ctx,PileType.Hand,1)>0)await Power<StrengthPower>(ctx,M);break;
            case "AttackStrength": await Attack(ctx,play);await Power<StrengthPower>(ctx,M);break;
            case "MarkedStrength":
                bool marked=target?.HasPower<RiftBurnPower>()==true;
                await Attack(ctx,play);if(marked)await Power<StrengthPower>(ctx,M);break;
            case "SanctuaryExhaust":
                await Block(play);await ExhaustChosen(ctx,PileType.Discard,(int)M);break;
            case "ForesightExhaust": await ExhaustChosen(ctx,PileType.Draw,(int)M);break;
            case "PlainAttack": await Attack(ctx,play); break;
            case "SpiritHalfAttack": await Attack(ctx,play,damage:DynamicVars.Damage.BaseValue+Owner.Creature.GetPowerAmount<SealProgressPower>()/2); break;
            case "AllAttackSpirit": await Attack(ctx,play,true); await Spirit(ctx,M); break;
            case "PulseMarks":
                await Attack(ctx,play,true);
                foreach(var enemy in CombatState!.HittableEnemies.ToArray())await Debuff<RiftBurnPower>(ctx,enemy,M);
                break;
            case "SealedMulti":
                var volley=Spec.Imprints?.FirstOrDefault(i=>i.Effect=="VolleyMarks"&&(i.Seal<=seals||_resolvingEdict||_forceImprint));
                for(int i=0;i<(int)E+seals/2 && target is {IsDead:false};i++) {
                    await Attack(ctx,play);
                    if(volley!=null) {
                        _imprintVolleyHits++;
                        await Debuff<RiftBurnPower>(ctx,target,volley.Value(IsUpgraded));
                        if(_resolvingEdict)await Debuff<RiftBurnPower>(ctx,target,volley.Value(IsUpgraded));
                    }
                }
                break;
            case "SealEchoDraw": await Draw(ctx,1+seals/2); break;
            case "VoidSpirit": await Power<IntangiblePower>(ctx,M); await Spirit(ctx,E); break;
            case "RebirthDraw": await Power<RebirthPower>(ctx,M); await Draw(ctx,E); break;
            case "AllMarks": foreach(var enemy in CombatState!.HittableEnemies.ToArray())await Debuff<RiftBurnPower>(ctx,enemy,M); break;
            case "ExhaustStatuses":
                var statuses=new[]{PileType.Draw,PileType.Discard,PileType.Hand}
                    .SelectMany(p=>CardPile.Get(p,Owner)!.Cards).Where(c=>c.Type==CardType.Status).Distinct().ToArray();
                foreach(var status in statuses) {
                    var result=await CardCmd.Exhaust(ctx,status,false,false);
                    if(result.HasValue && status.Pile?.Type==PileType.Exhaust)await Spirit(ctx,1);
                }
                break;
            case "MultiHit": for(int i=0;i<(int)E && target is { IsDead:false };i++) await Attack(ctx,play); break;
            case "Block": await Block(play); break;
            case "AttackSpirit": await Attack(ctx,play); await Spirit(ctx,M); break;
            case "SealGuard": await Block(play); if(seals>0)await Draw(ctx,1); break;
            case "Vulnerable": await Attack(ctx,play); await Debuff<VulnerablePower>(ctx,target,M); break;
            case "Draw": await Draw(ctx,M); break;
            case "AttackAll": await Attack(ctx,play,true); break;
            case "PurifyWeak": await Block(play); if(Owner.Creature.HasPower<WeakPower>())await PowerCmd.Remove<WeakPower>(Owner.Creature); break;
            case "Weak": await Attack(ctx,play); await Debuff<WeakPower>(ctx,target,M); break;
            case "Spirit": await Spirit(ctx,M); break;
            case "VeilWeak": await Debuff<WeakPower>(ctx,target,M); break;
            case "BurnSpirit": await BurnForSpirit(ctx); break;
            case "LifeSealPower": await Power<LifeSealPower>(ctx,M); break;
            case "SealAttack": await Attack(ctx,play,damage:DynamicVars.Damage.BaseValue+seals*M); break;
            case "SpiritAttack": await Attack(ctx,play,damage:DynamicVars.Damage.BaseValue+Owner.Creature.GetPowerAmount<SealProgressPower>()); break;
            case "BlockDraw": await Block(play); await Draw(ctx,M); break;
            case "AttackHeal": await Attack(ctx,play); await CreatureCmd.Heal(Owner.Creature,M); break;
            case "BlockDexterity": await Block(play); await Power<DexterityPower>(ctx,M); break;
            case "WeakBurn": await Debuff<WeakPower>(ctx,target,E); await Debuff<RiftBurnPower>(ctx,target,M); break;
            case "AttackBlock": await Attack(ctx,play); await Block(play); break;
            case "DrawSpirit": await Draw(ctx,M); await Spirit(ctx,E); break;
            case "CleanseDraw": await SealProgressPower.Cleanse(Owner.Creature); await Draw(ctx,M); break;
            case "MarkedAttack": await Attack(ctx,play,damage:DynamicVars.Damage.BaseValue+(target?.HasPower<RiftBurnPower>()==true?M:0)); break;
            case "Heal": await CreatureCmd.Heal(Owner.Creature,M); break;
            case "SealHeal": await CreatureCmd.Heal(Owner.Creature,M+seals*E); break;
            case "SpiritDexterity": await Power<DexterityPower>(ctx,1+Owner.Creature.GetPowerAmount<SealProgressPower>()/(int)E); break;
            case "SealBlock": await BlockAmount(play,DynamicVars.Block.BaseValue+seals*M); break;
            case "ConvertBlock":
                if(Owner.Creature.Block>=M) { await CreatureCmd.LoseBlock(ctx,Owner.Creature,M,Owner.Creature); await PlayerCmd.GainEnergy(1,Owner); await Spirit(ctx,1); } break;
            case "RadiantArmorPower": await Power<RadiantArmorPower>(ctx,M); break;
            case "SealMeditationPower": await Power<SealMeditationPower>(ctx,M); break;
            case "DawnRecoveryPower": await Power<DawnRecoveryPower>(ctx,M); break;
            case "PrismMindPower": await Power<PrismMindPower>(ctx,M); break;
            case "ThornsPower": await Power<ThornsPower>(ctx,M); break;
            case "HolyThornsPower": await Power<HolyThornsPower>(ctx,M); break;
            case "RiftMasteryPower": await Power<RiftMasteryPower>(ctx,M); break;
            case "PrismaticSkinPower": await Power<PrismaticSkinPower>(ctx,M); break;
            case "DexterityPower": await Power<DexterityPower>(ctx,M); break;
            case "EchoingLightPower": await Power<EchoingLightPower>(ctx,M); break;
            case "GatheringGlowPower": await Power<GatheringGlowPower>(ctx,M); break;
            case "SpiritDraw": await Spirit(ctx,M); await Draw(ctx,E); break;
            case "XSealBurst": await AccelerateSeals(ctx); break;
            case "XStrength": await Power<StrengthPower>(ctx,ResolveEnergyXValue()+M); break;
            case "Awakening": await Spirit(ctx,M); await PlayerCmd.GainEnergy(E,Owner); await Draw(ctx,E); break;
            case "RiftSingularity":
                foreach(var enemy in CombatState!.HittableEnemies.ToArray()) {
                    await Debuff<RiftBurnPower>(ctx,enemy,M);
                    await Debuff<RiftBurnPower>(ctx,enemy,(enemy.GetPowerAmount<RiftBurnPower>()/5)*3);
                }
                break;
            case "Reversal": await Block(play); await SealProgressPower.Cleanse(Owner.Creature); await Draw(ctx,M); break;
            case "HealEnergy": await CreatureCmd.Heal(Owner.Creature,M); await PlayerCmd.GainEnergy(E,Owner); break;
            case "AllFixed": await Attack(ctx,play,true); foreach(var enemy in CombatState!.HittableEnemies.ToArray())await Fixed(ctx,enemy,M); break;
            case "IntangibleBlock": await Power<IntangiblePower>(ctx,M); await Block(play); break;
            case "Edict": await Power<TemporaryEdictPower>(ctx,M); break;
            case "HealDraw": await CreatureCmd.Heal(Owner.Creature,M); await Draw(ctx,E); break;
            case "BlockSpirit": await Block(play); await Spirit(ctx,M); break;
            case "AttackAllWeak": await Attack(ctx,play,true); foreach(var enemy in CombatState!.HittableEnemies.ToArray())await Debuff<WeakPower>(ctx,enemy,M); break;
            case "BlockArtifact": await Block(play); await Power<ArtifactPower>(ctx,M); break;
            case "Detonate":
                var stacks=target?.GetPowerAmount<RiftBurnPower>()??0;
                await Attack(ctx,play);
                if(target is { IsDead:false }) { if(stacks>0)await PowerCmd.Remove<RiftBurnPower>(target); await Fixed(ctx,target,stacks*M); } break;
            case "SolarHeartPower":
                var heart=Owner.Creature.GetPower<SolarHeartPower>();
                if(heart==null)await Power<SolarHeartPower>(ctx,M);
                else if(heart.Amount<M)await PowerCmd.ModifyAmount(ctx,heart,M-heart.Amount,Owner.Creature,this);
                break;
            case "SealSovereignPower": await Power<SealSovereignPower>(ctx,M); break;
            case "CycleOfStarsPower": await Power<CycleOfStarsPower>(ctx,M); break;
            case "DivineBulwarkPower": await Power<DivineBulwarkPower>(ctx,M); break;
            case "PowerResonancePower": await Power<PowerResonancePower>(ctx,M); break;
            default: throw new InvalidOperationException("Unknown card rule: "+Spec.Rule);
        }
    }
}

