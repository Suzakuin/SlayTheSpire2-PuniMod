using static PuniMod.PuniFxShape;
using static PuniMod.PuniFxTone;

namespace PuniMod;

internal enum PuniFxShape
{
    Crescent, Flare, Leaves, Shield, Converge, Comet, Wave, Mark, Veil, Tether, Rift,
    Runes, Echo, Volley, Spear, Rain, Ash, Prism, Bolt, Growth, Hourglass, Gate,
    Orbit, Cross, Cleanse, Crown, Bloom, Singularity
}

internal enum PuniFxTone
{
    Gold, Ice, Pearl, Cream, Cyan, Mint, Magenta, Violet, Lilac, Indigo, Amber,
    Sky, Honey, Jade, Coral, Rose, Copper, Teal
}

internal readonly record struct PuniFxCue(PuniFxShape Shape, PuniFxTone Tone, bool AtTarget,
    int Count, float Angle, float Size, float Delay, bool RequiresMark = false);

internal sealed record PuniCardEffect(PuniFxCue[] Cast, PuniFxCue? Exhaust = null);

internal static class PuniCardEffects
{
    private static PuniFxCue S(PuniFxShape shape, PuniFxTone tone, int count, float angle,
        float size = 1f, float delay = 0f) => new(shape, tone, false, count, angle, size, delay);

    private static PuniFxCue H(PuniFxShape shape, PuniFxTone tone, int count, float angle,
        float size = 1f, float delay = 0f, bool requiresMark = false)
        => new(shape, tone, true, count, angle, size, delay, requiresMark);

    private static readonly Dictionary<Type, PuniCardEffect> Effects = new()
    {
        [typeof(StrikePuni)] = new([H(Crescent, Gold, 1, -26, .87f)]),
        [typeof(DefendPuni)] = new([S(Shield, Ice, 1, 0, .89f)]),
        [typeof(SacredLight)] = new([H(Flare, Pearl, 3, -12), S(Leaves, Cream, 1, -8, .70f)]),
        [typeof(SealRelease)] = new([S(Shield, Cyan, 2, 0, .81f), S(Converge, Gold, 3, -18, .92f, .075f)]),
        [typeof(Aurora)] = new([H(Comet, Mint, 1, -34, .74f)]),
        [typeof(ElementalPulse)] = new([H(Wave, Cyan, 2, -5, .88f), H(Mark, Magenta, 1, 15, .65f, .11f)]),
        [typeof(VoidVeil)] = new([H(Veil, Violet, 3, -22, .86f)]),
        [typeof(SpiritGather)] = new([S(Converge, Cyan, 5, 16, 1.01f)]),
        [typeof(SoulInterference)] = new([H(Tether, Lilac, 2, 0, .95f), S(Shield, Pearl, 1, 3, .61f)]),
        [typeof(SpaceRupture)] = new([H(Rift, Indigo, 1, -32, 1.09f)]),
        [typeof(SacredWard)] = new([S(Shield, Pearl, 1, 0, 1.04f), S(Runes, Gold, 3, 10, .72f, .10f)]),
        [typeof(LightWrath)] = new([H(Flare, Amber, 6, 22, 1.20f)]),
        [typeof(SealEcho)] = new([S(Echo, Gold, 2, 64, .88f)], S(Ash, Amber, 1, -24, .88f)),
        [typeof(RadiantWave)] = new([S(Wave, Cyan, 3, 90, 1.13f)]),
        [typeof(TwinRay)] = new([H(Volley, Cream, 2, 26, .83f)]),
        [typeof(LightStep)] = new([S(Comet, Sky, 3, 0, .83f)]),
        [typeof(FocusedSpark)] = new([H(Converge, Honey, 4, 21, .80f), H(Flare, Pearl, 1, 0, .70f, .09f)]),
        [typeof(SealGuard)] = new([S(Shield, Sky, 2, 12, .82f), S(Leaves, Pearl, 1, 12, .64f, .05f)]),
        [typeof(ShatterRay)] = new([H(Spear, Gold, 2, -8, .98f), H(Rift, Cyan, 2, 45, .63f, .07f)]),
        [typeof(SpiritSurvey)] = new([S(Leaves, Sky, 2, -16, 1.05f)]),
        [typeof(Starfall)] = new([H(Rain, Indigo, 3, -15, 1.08f)]),
        [typeof(PurifyingBreath)] = new([S(Shield, Mint, 1, 0, 1.10f)], S(Ash, Pearl, 3, 10, 1.04f)),
        [typeof(SlowPulse)] = new([H(Wave, Violet, 3, -26, .82f)]),
        [typeof(HiddenEdge)] = new([S(Spear, Amber, 1, -60, .60f), S(Converge, Gold, 2, 25, .72f)]),
        [typeof(VoidSeal)] = new([S(Veil, Indigo, 2, 20, 1.16f), S(Runes, Lilac, 1, 5, .72f, .115f)]),
        [typeof(ElementSeal)] = new([H(Mark, Magenta, 3, -20, .98f), S(Prism, Cyan, 3, 24, .72f)]),
        [typeof(EnergySeal)] = new([S(Shield, Amber, 2, 23, 1.02f), S(Bolt, Honey, 3, 4, 1.05f, .07f)]),
        [typeof(LifeSeal)] = new([S(Growth, Jade, 3, -25, 1.10f), S(Runes, Gold, 4, 8, .68f, .095f)]),
        [typeof(Reincarnation)] = new([S(Comet, Coral, 2, -30, 1.12f), S(Flare, Cream, 4, 0, .77f, .125f)]),
        [typeof(Eternity)] = new([S(Hourglass, Indigo, 1, 0, 1.07f), S(Leaves, Pearl, 3, 0, .74f, .055f)]),
        [typeof(Purity)] = new([S(Cleanse, Pearl, 5, 15, 1.14f)]),
        [typeof(HolyGate)] = new([H(Gate, Honey, 2, 0, 1.15f), S(Flare, Gold, 3, 14, .79f, .04f)]),
        [typeof(WhirlingRift)] = new([H(Rift, Magenta, 3, 26, 1.18f), H(Orbit, Cyan, 2, -25, .78f, .04f)]),
        [typeof(ThousandRays)] = new([H(Rain, Lilac, 6, 34, 1.16f), S(Cleanse, Pearl, 3, 0, .70f)]),
        [typeof(EternalCycle)] = new([S(Orbit, Indigo, 2, 40, 1.01f), S(Hourglass, Gold, 1, 8, .69f, .12f)]),
        [typeof(SealResonance)] = new([S(Shield, Cyan, 3, 13, .95f), S(Echo, Gold, 3, 90, .90f, .12f)]),
        [typeof(SealSpear)] = new([H(Spear, Gold, 4, -14, 1.20f), H(Runes, Cyan, 3, 14, .78f, .075f)]),
        [typeof(Sanctuary)] = new([S(Gate, Mint, 2, 0, 1.22f), S(Shield, Pearl, 1, 0, .87f, .05f)], S(Ash, Jade, 3, -58, 1.14f)),
        [typeof(SpiritSiphon)] = new([H(Tether, Jade, 3, 17, 1.10f)], S(Ash, Coral, 1, 30, 1.00f)),
        [typeof(PrismVolley)] = new([H(Volley, Cyan, 3, -14, 1.01f), H(Prism, Lilac, 3, 0, .66f, .095f)]),
        [typeof(DawnGuard)] = new([S(Shield, Honey, 1, 15, 1.17f), S(Rain, Pearl, 3, 0, .70f, .04f)]),
        [typeof(ElementBreak)] = new([H(Prism, Coral, 5, -20, .95f), H(Rift, Mint, 2, 35, .69f, .06f)]),
        [typeof(SoulAnchor)] = new([H(Gate, Violet, 1, 0, .86f), H(Mark, Lilac, 2, -23, .92f, .07f)]),
        [typeof(OrbitingLight)] = new([H(Comet, Gold, 2, 37, 1.11f), S(Orbit, Mint, 1, -60, .69f, .11f)]),
        [typeof(SealForesight)] = new([S(Leaves, Lilac, 3, 21, 1.13f)], S(Ash, Violet, 3, 46, .98f)),
        [typeof(QuietMind)] = new([S(Cleanse, Mint, 3, -33, .94f)], S(Ash, Mint, 2, 0, .84f)),
        [typeof(CelestialCross)] = new([H(Cross, Gold, 2, 0, 1.25f), H(Mark, Magenta, 1, 45, .72f, .10f)]),
        [typeof(DispelPulse)] = new([H(Wave, Amber, 2, 5, 1.15f), H(Veil, Indigo, 1, -5, .67f, .075f)]),
        [typeof(RenewedVigor)] = new([S(Bolt, Jade, 4, 15, 1.12f)]),
        [typeof(SevenfoldShield)] = new([S(Shield, Cyan, 3, 28, 1.16f), S(Runes, Gold, 7, 0, 1.07f, .09f)]),
        [typeof(FlowingThought)] = new([S(Leaves, Amber, 2, 38, .97f), S(Comet, Gold, 1, -20, .82f, .11f)]),
        [typeof(SealConversion)] = new([S(Converge, Amber, 3, 38, .81f)], S(Ash, Honey, 1, -20, 1.18f)),
        [typeof(RadiantArmor)] = new([S(Prism, Gold, 4, 18, 1.13f), S(Shield, Amber, 2, 8, .98f, .11f)]),
        [typeof(SealMeditation)] = new([S(Orbit, Mint, 3, 11, .98f), S(Converge, Gold, 1, 0, .70f)]),
        [typeof(DawnRecovery)] = new([S(Growth, Honey, 4, 30, 1.02f), S(Shield, Coral, 1, 0, .77f, .10f)]),
        [typeof(PrismMind)] = new([S(Prism, Amber, 3, 45, 1.22f), S(Leaves, Mint, 2, -6, .71f, .10f)]),
        [typeof(HolyThorns)] = new([S(Growth, Gold, 6, 75, 1.08f), S(Mark, Magenta, 3, 0, .78f)]),
        [typeof(RiftMastery)] = new([S(Rift, Violet, 2, -68, .97f), S(Ash, Amber, 3, 10, .74f, .135f)]),
        [typeof(PrismaticSkin)] = new([S(Prism, Cyan, 5, 5, 1.21f), S(Shield, Lilac, 1, 27, .82f, .13f)]),
        [typeof(Stillness)] = new([S(Converge, Sky, 2, -55, .78f), S(Echo, Pearl, 1, 90, .77f)]),
        [typeof(EchoingLight)] = new([S(Echo, Sky, 4, -14, 1.02f), S(Mark, Coral, 1, 25, .76f, .11f)]),
        [typeof(GatheringGlow)] = new([S(Echo, Honey, 3, 70, 1.23f), S(Runes, Copper, 4, 24, .84f, .09f)]),
        [typeof(HolyLightQi)] = new([S(Bolt, Gold, 5, 0, 1.28f), S(Flare, Sky, 5, 0, .84f, .10f)]),
        [typeof(HolySpiritFlash)] = new([S(Runes, Pearl, 7, 25, .85f)]),
        [typeof(SevenSeals)] = new([S(Runes, Lilac, 7, 0, 1.29f), S(Converge, Gold, 7, 32, .92f, .08f)]),
        [typeof(TrueManifestation)] = new([S(Crown, Pearl, 7, 0, 1.33f), S(Shield, Gold, 3, 0, 1.25f, .15f)]),
        [typeof(BrilliantHolyLight)] = new([S(Veil, Rose, 4, 8, 1.14f), S(Growth, Pearl, 5, 8, .96f, .10f)]),
        [typeof(HeavenlyBloom)] = new([H(Bloom, Rose, 6, 15, 1.18f)]),
        [typeof(SoulRequiem)] = new([H(Echo, Lilac, 3, -25, 1.14f), S(Gate, Teal, 2, 0, 1.13f)]),
        [typeof(HolyShadowBreak)] = new([H(Crescent, Indigo, 2, -32, 1.19f), S(Growth, Gold, 2, -15, .83f, .07f)]),
        [typeof(DawnJudgment)] = new([H(Rain, Gold, 1, 0, 1.45f), H(Cross, Cream, 1, 4, .87f, .16f)]),
        [typeof(SealAcceleration)] = new([S(Gate, Cyan, 3, 0, .94f), S(Leaves, Gold, 5, -38, 1.13f, .11f)]),
        [typeof(SacredReversal)] = new([S(Shield, Rose, 3, -16, 1.12f), S(Cleanse, Pearl, 4, 180, .97f, .05f)]),
        [typeof(EternalDawn)] = new([S(Gate, Amber, 3, -9, .93f)], S(Ash, Coral, 7, 92, 1.44f)),
        [typeof(Singularity)] = new([H(PuniFxShape.Singularity, Teal, 4, -5, 1.23f)]),
        [typeof(SevenfoldStrike)] = new([H(Volley, Gold, 7, 7, 1.14f), H(Cross, Cream, 1, 45, .73f, .20f)]),
        [typeof(VoidSanctuary)] = new([S(Veil, Violet, 5, 0, 1.41f), S(Shield, Lilac, 2, 0, .98f, .09f)]),
        [typeof(ShiningEdict)] = new([S(Runes, Rose, 2, 9, 1.15f), S(Cross, Gold, 2, 16, .72f, .115f)]),
        [typeof(SoulReturn)] = new([S(Growth, Mint, 2, 12, 1.17f), S(Comet, Honey, 1, 31, .93f, .095f)]),
        [typeof(SealAwakening)] = new([S(Runes, Gold, 3, 85, 1.19f), S(Bolt, Honey, 2, 0, .83f, .08f)]),
        [typeof(StellarSweep)] = new([H(Comet, Indigo, 3, -5, 1.26f), H(Wave, Rose, 1, 29, 1.02f, .075f)]),
        [typeof(PerfectSeal)] = new([S(Gate, Copper, 3, 0, 1.16f), S(Shield, Pearl, 3, 0, 1.03f, .115f)]),
        [typeof(LightOfCreation)] = new([H(Bloom, Gold, 5, -33, 1.30f), H(Bolt, Pearl, 2, 63, .83f, .10f)]),
        [typeof(FinalRadiance)] = new([H(Rift, Magenta, 5, -35, 1.36f), H(Flare, Gold, 7, 2, 1.10f, .13f, requiresMark: true)]),
        [typeof(SolarHeart)] = new([S(Crown, Coral, 7, 17, 1.35f), S(Orbit, Amber, 2, -30, .91f)]),
        [typeof(SealSovereign)] = new([S(Crown, Gold, 7, 39, 1.22f), S(Runes, Cyan, 6, 60, 1.13f, .09f)]),
        [typeof(CycleOfStars)] = new([S(Growth, Teal, 3, 43, 1.17f), S(Cross, Gold, 3, 25, .92f, .14f)]),
        [typeof(DivineBulwark)] = new([S(Shield, Honey, 3, 34, 1.31f), S(Growth, Pearl, 4, 0, .92f, .12f)]),
        [typeof(PowerResonance)] = new([S(Echo, Jade, 3, -67, 1.16f), S(Bolt, Gold, 3, 45, .87f, .09f)]),
        [typeof(HolyOrigin)] = new([H(Crown, Pearl, 4, 180, 1.37f), H(Flare, Cyan, 6, 20, 1.16f, .14f), S(Leaves, Gold, 2, -25, .93f)]),
        [typeof(BookOfEternity)] = new([S(Leaves, Honey, 4, 0, 1.27f), S(Runes, Pearl, 7, -16, 1.48f, .13f)]),
    };

    private static readonly PuniCardEffect Default = new([S(Flare, Pearl, 2, 0, .75f)]);
    internal static PuniCardEffect For(Type type) => Effects.GetValueOrDefault(type, Default);
    internal static IReadOnlyDictionary<Type, PuniCardEffect> All => Effects;
}
