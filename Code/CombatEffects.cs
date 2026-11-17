using Godot;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace PuniMod;

internal static class PuniCombatEffects
{
    private const string WheelName = "PuniSealWheel";
    private static readonly ConditionalWeakTable<NCreature, PuniSealWheel> Wheels = new();

    public static void Card(PuniCard card, CardPlay play)
    {
        var owner = card.Owner.Creature;
        Wheel(owner);
        if (owner.GetVfxContainer()?.IsInsideTree() != true) return;
        foreach (var profile in PuniCardEffects.For(card.GetType()).Cast)
        {
            if (profile.RequiresMark && play.Target?.HasPower<RiftBurnPower>() != true) continue;
            var cue = profile;
            if (cue.Shape == PuniFxShape.Volley)
            {
                int count = (int)card.DynamicVars["Extra"].BaseValue;
                if (card.Spec.Rule == "SealedMulti") count += card.ActiveSealCount / 2;
                cue = cue with { Count = Math.Clamp(count, 1, 12) };
            }
            if (!cue.AtTarget) PlayCue(owner, cue, card.GetType().Name);
            else if (card.TargetType == TargetType.AllEnemies && owner.CombatState is {} combat)
            {
                foreach (var enemy in combat.HittableEnemies)
                    PlayCue(enemy, cue, card.GetType().Name, owner);
            }
            else if (play.Target is {} target)
                PlayCue(target, cue, card.GetType().Name, owner);
        }
    }

    public static void Exhausted(PuniCard card, int count)
    {
        if (count <= 0 || card.Owner.Creature.GetVfxContainer()?.IsInsideTree() != true) return;
        if (PuniCardEffects.For(card.GetType()).Exhaust is {} cue)
            PlayCue(card.Owner.Creature, cue with { Count = Math.Clamp(count, 1, 7) }, card.GetType().Name);
    }

    private static void PlayCue(Creature anchor, PuniFxCue cue, string card, Creature? owner = null)
    {
        if (!TryLocate(anchor, owner, out var layer, out var at, out var other)) return;
        var view = new PuniSignatureEffect(cue, other).View;
        view.Name = "Puni" + cue.Shape;
        view.Position = at;
        view.SetMeta("puni_effect", cue.Shape.ToString());
        view.SetMeta("puni_card", card);
        layer.AddChild(view);
    }

    public static void Seals(Creature owner, int first, int last)
        => Wheel(owner)?.Release(first, last);

    public static void Imprint(Creature owner, int seal, bool temporary)
        => Wheel(owner)?.Flash(seal, temporary);

    public static bool CanAim(Creature owner, Creature target)
        => owner.GetVfxContainer()?.IsInsideTree() == true
           && owner.GetCreatureNode()?.IsVisibleInTree() == true
           && target.GetCreatureNode()?.IsVisibleInTree() == true;

    public static Node2D? Charge(Creature owner, Creature target)
        => Create(owner, PuniEffectStyle.Charge, target);

    public static Node2D? Beam(Creature owner, Creature target)
        => Create(target, PuniEffectStyle.Beam, owner);

    public static void HolyImpact(Creature target)
        => Add(target, PuniEffectStyle.HolyImpact);

    private static PuniSealWheel? Wheel(Creature owner)
    {
        var node = owner.GetCreatureNode();
        if (node == null || !node.IsInsideTree() || node.IsQueuedForDeletion()) return null;
        return Wheels.GetValue(node, creature =>
        {
            var wheel = new PuniSealWheel(creature);
            wheel.View.Name = WheelName;
            creature.Visuals.AddChild(wheel.View);
            creature.Visuals.MoveChild(wheel.View, 0);
            return wheel;
        });
    }

    private static void Add(Creature anchor, PuniEffectStyle style, Creature? other = null)
    {
        if (Create(anchor, style, other) is {} effect)
            anchor.GetVfxContainer()?.AddChild(effect);
    }

    private static Node2D? Create(Creature anchor, PuniEffectStyle style, Creature? other)
    {
        if (!TryLocate(anchor, other, out _, out var at, out var from)) return null;
        var effect = new PuniCastEffect(style, from);
        effect.View.Name = "Puni" + style;
        effect.View.Position = at;
        effect.View.SetMeta("puni_effect", style.ToString());
        return effect.View;
    }

    private static bool TryLocate(Creature anchor, Creature? other, out Control layer, out Vector2 at, out Vector2 from)
    {
        layer = anchor.GetVfxContainer()!;
        var node = anchor.GetCreatureNode();
        at = Vector2.Zero;
        from = new Vector2(-130, 0);
        if (layer == null || node == null || !layer.IsInsideTree() || !node.IsVisibleInTree()
            || node.IsQueuedForDeletion() || anchor.IsDead)
            return false;

        var inverse = layer.GetGlobalTransform().AffineInverse();
        at = inverse * node.VfxSpawnPosition;
        if (other?.GetCreatureNode() is {} otherNode && otherNode.IsInsideTree())
            from = inverse * otherNode.VfxSpawnPosition - at;
        return true;
    }
}

internal enum PuniEffectStyle { Charge, Beam, HolyImpact }

internal abstract class PuniEffectDrawing
{
    public Node2D View { get; } = new();
    protected static readonly Color Gold = new("ffd379");
    protected static readonly Color White = new("fff4cf");
    protected static readonly Color Blue = new("37ccf3");
    protected static readonly Color Mint = new("70ebbd");

    protected PuniEffectDrawing() => View.Draw += Draw;
    protected abstract void Draw();

    protected void DrawCircle(Vector2 at, float radius, Color color) => View.DrawCircle(at, radius, color);
    protected void DrawLine(Vector2 from, Vector2 to, Color color, float width, bool antialias)
        => View.DrawLine(from, to, color, width, antialias);
    protected void DrawArc(Vector2 at, float radius, float start, float end, int segments, Color color, float width, bool antialias)
        => View.DrawArc(at, radius, start, end, segments, color, width, antialias);
    protected void DrawPolyline(Vector2[] points, Color color, float width, bool antialias)
        => View.DrawPolyline(points, color, width, antialias);

    protected static Color Alpha(Color color, float amount) => new(color, Mathf.Clamp(amount, 0f, 1f));

    protected void Ellipse(Vector2 center, Vector2 radii, float start, float end, Color color, float width)
    {
        const int segments = 44;
        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
            points[i] = center + Vector2.FromAngle(Mathf.Lerp(start, end, i / (float)segments)) * radii;
        DrawPolyline(points, color, width, true);
    }

    protected void Diamond(Vector2 at, Vector2 radii, Color color)
        => View.DrawColoredPolygon([at - Vector2.Down * radii.Y, at + Vector2.Right * radii.X,
            at + Vector2.Down * radii.Y, at - Vector2.Right * radii.X], color);

    protected void Trace(Vector2[] points, Color color, float opacity, float width)
    {
        DrawPolyline(points, Alpha(color, opacity * .19f), width + 10f, true);
        DrawPolyline(points, Alpha(color, opacity * .55f), width + 3f, true);
        DrawPolyline(points, Alpha(White, opacity), width, true);
    }
}

internal sealed class PuniSealWheel : PuniEffectDrawing
{
    private static readonly Color[] Colors =
        [new("b3a0ff"), new("5de7ff"), new("ffbd67"), new("77e8ac"), new("ff8b9b"), new("98aaff"), White];
    private readonly float[] _reveals = new float[7];
    private readonly float[] _flashes = new float[7];
    private readonly float[] _earlierFlashes = new float[7];
    private readonly bool[] _borrowed = new bool[7];
    private readonly NCreature _creature;
    private readonly Sprite2D? _body;
    private readonly ulong _started = Time.GetTicksMsec();
    private Tween? _animation;
    private float Clock => (Time.GetTicksMsec() - _started) / 1000f;

    public PuniSealWheel(NCreature creature)
    {
        _creature = creature;
        _body = creature.Visuals.GetNodeOrNull<Sprite2D>("%Visuals");
        View.Position = creature.Visuals.VfxSpawnPosition.Position + new Vector2(0, -40);
        Array.Fill(_reveals, float.NegativeInfinity);
        Array.Fill(_flashes, float.NegativeInfinity);
        Array.Fill(_earlierFlashes, float.NegativeInfinity);
    }

    public void Release(int first, int last)
    {
        if (first > last)
        {
            View.QueueRedraw();
            return;
        }
        float now = Clock;
        for (int seal = first; seal <= last; seal++)
            _reveals[seal - 1] = now + .075f * (seal - first);
        Animate();
    }

    public void Flash(int seal, bool temporary)
    {
        int slot = seal - 1;
        float now = Clock;
        if (now - _flashes[slot] < .13f && _flashes[slot] <= now)
        {
            _earlierFlashes[slot] = _flashes[slot];
            _flashes[slot] += .13f;
        }
        else if (_flashes[slot] <= now)
        {
            _earlierFlashes[slot] = float.NegativeInfinity;
            _flashes[slot] = now;
        }
        _borrowed[slot] = temporary;
        Animate();
    }

    private void Animate()
    {
        _animation?.Kill();
        _animation = View.CreateTween();
        _animation.TweenMethod(Callable.From<float>(_ => View.QueueRedraw()), 0f, 1f, 1.2);
        View.QueueRedraw();
    }

    protected override void Draw()
    {
        var owner = _creature.Entity;
        if (owner.IsDead || owner.CombatState == null) return;
        int unlocked = SealProgressPower.Unlocked(owner);
        float now = Clock;
        float sanctity = Burst(now - _reveals[6], .69f);
        var radii = new Vector2(162 + sanctity * 16, 131 + sanctity * 12);
        Ellipse(Vector2.Zero, radii, 0, Mathf.Tau, Alpha(Blue, .19f + sanctity * .40f), 2);
        Ellipse(Vector2.Zero, radii + new Vector2(9, 8), -.9f, 2.4f, Alpha(Gold, .15f + sanctity * .42f), 1);
        Ellipse(Vector2.Zero, radii + new Vector2(9, 8), 2.65f, 5.2f, Alpha(Gold, .15f + sanctity * .42f), 1);

        for (int i = 0; i < 7; i++)
        {
            float angle = -Mathf.Pi / 2 + Mathf.Tau * i / 7;
            var at = radii * Vector2.FromAngle(angle);
            float reveal = Burst(now - _reveals[i], i == 6 ? .69f : .39f);
            float flash = Math.Max(Burst(now - _flashes[i], .23f), Burst(now - _earlierFlashes[i], .23f));
            bool lit = i < unlocked && now >= _reveals[i];
            float intensity = Math.Max(reveal, flash);
            float light = Math.Max(intensity, sanctity * .70f);
            if (light > 0)
            {
                DrawCircle(at, 26 + 8 * light, Alpha(Colors[i], .17f * light));
                DrawArc(at, 19 + (1 - light) * 10, 0, Mathf.Tau, 28, Alpha(Colors[i], .74f * light), 2, true);
            }
            DrawCircle(at, 13 + 3 * light, new Color(.039f, .077f, .12f, .82f));
            Color tint = _borrowed[i] && flash > 0 ? new Color("e4bcff") : Colors[i];
            float alpha = lit ? .76f : .31f;
            DrawArc(at, 13 + 3 * light, 0, Mathf.Tau, 25, Alpha(tint, alpha + light), 1.7f, true);
            Rune(at, i, Alpha(tint, alpha + light), 1 + light * .22f);

            if (reveal > 0) ReleaseFlourish(i, at, reveal);
        }
    }

    private static float Burst(float elapsed, float duration)
        => elapsed < 0 || elapsed >= duration ? 0f : 1f - elapsed / duration;

    private void Rune(Vector2 at, int index, Color color, float size)
    {
        // Seven distinct marks stay distinguishable even when the color is dim.
        var points = index switch
        {
            0 => new[] { new Vector2(-6, 2), new(-3, -4), new(2, -5), new(6, 1), new(2, 5), new(-2, 4) },
            1 => new[] { new Vector2(-7, 3), new(-3, -4), new(0, 2), new(3, -4), new(7, 3) },
            2 => new[] { new Vector2(2, -7), new(-4, 0), new(3, 0), new(-2, 7) },
            3 => new[] { new Vector2(0, 7), new(0, -6), new(-5, 1), new(0, 3), new(5, -1) },
            4 => new[] { new Vector2(-5, 1), new(-3, 6), new(3, 6), new(6, 1), new(2, -5), new(-4, -5) },
            5 => new[] { new Vector2(-6, -6), new(6, -6), new(-6, 6), new(6, 6), new(-6, -6) },
            _ => new[] { new Vector2(0, -8), new(2, -2), new(8, 0), new(2, 2), new(0, 8),
                new(-2, 2), new(-8, 0), new(-2, -2), new(0, -8) },
        };
        for (int i = 0; i < points.Length; i++) points[i] = at + points[i] * size;
        DrawPolyline(points, color, 1.7f, true);
    }

    private void ReleaseFlourish(int index, Vector2 at, float fade)
    {
        Color color = Colors[index];
        float progress = 1 - fade;
        if (index == 0 && _body?.Texture is {} texture)
        {
            int columns = _body.Hframes;
            var size = texture.GetSize() / new Vector2(columns, _body.Vframes);
            var frame = new Vector2(_body.Frame % columns, _body.Frame / columns) * size;
            var rect = _body.GetRect();
            var bounds = new Rect2(_body.Position - View.Position + rect.Position * _body.Scale, rect.Size * _body.Scale);
            var drift = new Vector2(38 + 37 * progress, -18 * progress);
            View.DrawTextureRectRegion(texture, new Rect2(bounds.Position + drift, bounds.Size), new Rect2(frame, size), Alpha(color, fade * .28f));
            View.DrawTextureRectRegion(texture, new Rect2(bounds.Position - drift, bounds.Size), new Rect2(frame, size), Alpha(color, fade * .19f));
        }
        else if (index == 3 || index == 6)
        {
            int branches = index == 6 ? 3 : 2;
            for (int side = -1; side <= 1; side += 2)
            for (int branch = 0; branch < branches; branch++)
            {
                float spread = 100 + branch * 24;
                var root = new Vector2(0, 36);
                var tip = new Vector2(side * spread, -102 + branch * 38);
                var curve = new Vector2(side * (49 + branch * 18), -10 + branch * 23);
                var points = new Vector2[13];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = Mathf.Min(1f, progress * 2.6f) * i / (points.Length - 1);
                    points[i] = root.Lerp(curve, t).Lerp(curve.Lerp(tip, t), t);
                }
                Trace(points, color, fade * .9f, 2.5f);
            }
        }
        else
        {
            var radii = new Vector2(39, 20) * (1 + progress * 1.8f);
            float axis = at.Angle() + progress;
            Ellipse(at * fade, radii, axis, axis + Mathf.Pi * 1.42f, Alpha(color, fade * .82f), 2.4f);
        }

        for (int i = 0; i < 8; i++)
        {
            var ray = Vector2.FromAngle(i * Mathf.Tau / 8 + index * .31f);
            Diamond(at + ray * (22 + 39 * progress), new Vector2(2.1f, 5) * fade, Alpha(color, fade));
        }
    }
}

internal sealed class PuniCastEffect : PuniEffectDrawing
{
    private readonly PuniEffectStyle _style;
    private readonly Vector2 _other;
    private float _time;

    public PuniCastEffect(PuniEffectStyle style, Vector2 other)
    {
        _style = style;
        _other = other;
        float duration = style switch
        {
            PuniEffectStyle.Charge => .27f,
            PuniEffectStyle.Beam => .36f,
            _ => .31f,
        };
        View.TreeEntered += () =>
        {
            var tween = View.CreateTween();
            tween.TweenMethod(Callable.From<float>(t => { _time = t; View.QueueRedraw(); }), 0f, 1f, duration);
            tween.TweenCallback(Callable.From(View.QueueFree));
        };
    }

    protected override void Draw()
    {
        float t = _time;
        float fade = Mathf.Clamp((1f - t) * 1.65f, 0f, 1f);
        switch (_style)
        {
            case PuniEffectStyle.Charge: Charge(t, fade); break;
            case PuniEffectStyle.Beam: Beam(t, fade); break;
            case PuniEffectStyle.HolyImpact: Impact(t, fade, White, 1.45f); break;
        }
    }

    private void Charge(float t, float fade)
    {
        var aim = _other.Normalized();
        var across = aim.Rotated(Mathf.Pi / 2);
        var at = aim * 34;
        float radius = Mathf.Lerp(118, 25, t * t);
        for (int side = -1; side <= 1; side += 2)
        for (int fork = 0; fork < 3; fork++)
        {
            var far = at - aim * (radius * (.55f + fork * .15f)) + across * (side * radius * (1f - fork * .25f));
            var mid = at - aim * radius * .55f + across * (side * radius * (.25f - fork * .04f));
            var points = new Vector2[15];
            for (int i = 0; i < points.Length; i++)
            {
                float n = i / (float)(points.Length - 1);
                points[i] = far.Lerp(mid, n).Lerp(mid.Lerp(at, n), n);
            }
            Trace(points, fork == 0 ? Blue : Gold, .85f, 2.7f);
        }
        DrawCircle(at, 23 + 15 * t, Alpha(Blue, fade * .18f));
        Diamond(at, new Vector2(9, 24) * (1 + t), Alpha(White, fade));
    }

    private void Beam(float t, float fade)
    {
        var axis = _other.Normalized();
        var across = new Vector2(-axis.Y, axis.X);
        var source = _other - axis * 35;
        DrawLine(source, Vector2.Zero, Alpha(Blue, .21f * fade), 78 * fade, true);
        DrawLine(source, Vector2.Zero, Alpha(Gold, .53f * fade), 35 * fade, true);
        DrawLine(source, Vector2.Zero, Alpha(White, .96f * fade), 14 * fade, true);
        for (int strand = 0; strand < 2; strand++)
        {
            var points = new Vector2[36];
            for (int i = 0; i < points.Length; i++)
            {
                float n = i / (float)(points.Length - 1);
                float twist = Mathf.Sin(n * 14f - t * 12 + strand * Mathf.Pi);
                points[i] = source.Lerp(Vector2.Zero, n) + across * twist * (12 + 25 * Mathf.Sin(n * Mathf.Pi)) * fade;
            }
            Trace(points, strand == 0 ? Blue : Gold, fade, 2.4f);
        }
        Impact(t, fade, Gold, 1.16f);
    }

    private void Impact(float t, float fade, Color color, float size)
    {
        Ellipse(Vector2.Zero, new Vector2(19 + 61 * t, 13 + 43 * t) * size, 0, Mathf.Tau,
            Alpha(color, fade * .85f), 2.5f);
        for (int i = 0; i < 8; i++)
        {
            var ray = Vector2.FromAngle(i * Mathf.Tau / 8 + .23f);
            var at = ray * (15 + 83 * t) * size;
            DrawLine(at, at + ray * (22 * fade * size), Alpha(color, fade), 2.7f, true);
        }
        Diamond(Vector2.Zero, new Vector2(9, 29) * (fade * size), Alpha(White, fade));
    }
}
