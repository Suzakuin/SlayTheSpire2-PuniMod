using Godot;

namespace PuniMod;

internal sealed class PuniSignatureEffect : PuniEffectDrawing
{
    private readonly PuniFxCue _cue;
    private readonly Color _color;
    private readonly Vector2 _source;
    private float _time;
    private int Count => _cue.Count;

    public PuniSignatureEffect(PuniFxCue cue, Vector2 from)
    {
        _cue = cue;
        _color = Tone(cue.Tone);
        float angle = Mathf.DegToRad(cue.Angle);
        View.Rotation = angle;
        View.Scale = Vector2.One * cue.Size;
        _source = from.Rotated(-angle) / cue.Size;
        View.Visible = cue.Delay <= 0f;
        View.TreeEntered += () =>
        {
            var tween = View.CreateTween();
            if (cue.Delay > 0f)
            {
                tween.TweenInterval(cue.Delay);
                tween.TweenCallback(Callable.From(View.Show));
            }
            tween.TweenMethod(Callable.From<float>(time =>
            {
                _time = time;
                View.QueueRedraw();
            }), 0f, 1f, Duration(cue));
            tween.TweenCallback(Callable.From(View.QueueFree));
        };
    }

    private static float Duration(PuniFxCue cue) => cue.Shape switch
    {
        PuniFxShape.Volley => .36f + .022f * cue.Count,
        PuniFxShape.Gate or PuniFxShape.Crown or PuniFxShape.Singularity => .56f,
        PuniFxShape.Shield or PuniFxShape.Growth or PuniFxShape.Runes or PuniFxShape.Ash => .48f,
        PuniFxShape.Comet or PuniFxShape.Spear or PuniFxShape.Crescent => .30f,
        _ => .39f,
    };

    private static Color Tone(PuniFxTone tone) => new(tone switch
    {
        PuniFxTone.Gold => "eab75e", PuniFxTone.Ice => "b7e8ff", PuniFxTone.Pearl => "fff3db",
        PuniFxTone.Cream => "ffe3aa", PuniFxTone.Cyan => "56daed", PuniFxTone.Mint => "9af6d2",
        PuniFxTone.Magenta => "c377d2", PuniFxTone.Violet => "9475db", PuniFxTone.Lilac => "b5abff",
        PuniFxTone.Indigo => "6f82eb", PuniFxTone.Amber => "ffa84f", PuniFxTone.Sky => "8bbbf8",
        PuniFxTone.Honey => "ffce77", PuniFxTone.Jade => "62d8ab", PuniFxTone.Coral => "ff886c",
        PuniFxTone.Rose => "f5a9c3", PuniFxTone.Copper => "e99c70", _ => "49c9b8",
    });

    protected override void Draw()
    {
        float t = _time;
        float strength = Envelope(t);
        if (strength <= .001f) return;
        switch (_cue.Shape)
        {
            case PuniFxShape.Crescent: Crescent(t, strength); break;
            case PuniFxShape.Flare: Flare(t, strength); break;
            case PuniFxShape.Leaves: Leaves(t, strength); break;
            case PuniFxShape.Shield: Shield(t, strength); break;
            case PuniFxShape.Converge: Converge(t, strength); break;
            case PuniFxShape.Comet: Comet(t, strength); break;
            case PuniFxShape.Wave: Wave(t, strength); break;
            case PuniFxShape.Mark: Marks(t, strength); break;
            case PuniFxShape.Veil: Veil(t, strength); break;
            case PuniFxShape.Tether: Tether(t, strength); break;
            case PuniFxShape.Rift: Rift(t, strength); break;
            case PuniFxShape.Runes: Runes(t, strength); break;
            case PuniFxShape.Echo: Echo(t, strength); break;
            case PuniFxShape.Volley: Volley(t); break;
            case PuniFxShape.Spear: Spear(t, strength); break;
            case PuniFxShape.Rain: Rain(t, strength); break;
            case PuniFxShape.Ash: Ash(t, strength); break;
            case PuniFxShape.Prism: Prism(t, strength); break;
            case PuniFxShape.Bolt: Bolt(t, strength); break;
            case PuniFxShape.Growth: Growth(t, strength); break;
            case PuniFxShape.Hourglass: Hourglass(t, strength); break;
            case PuniFxShape.Gate: Gate(t, strength); break;
            case PuniFxShape.Orbit: Orbit(t, strength); break;
            case PuniFxShape.Cross: Cross(t, strength); break;
            case PuniFxShape.Cleanse: Cleanse(t, strength); break;
            case PuniFxShape.Crown: Crown(t, strength); break;
            case PuniFxShape.Bloom: Bloom(t, strength); break;
            case PuniFxShape.Singularity: Singularity(t, strength); break;
        }
    }

    private static float Envelope(float t) => Mathf.SmoothStep(0f, .16f, t) * (1f - Mathf.SmoothStep(.38f, 1f, t));
    private static float Spread(int index, int count, float low, float high)
        => count <= 1 ? (low + high) * .5f : Mathf.Lerp(low, high, index / (float)(count - 1));

    private void Edge(Vector2[] points, float strength, float width = 2) => Trace(points, _color, strength, width);
    private void Facet(Vector2[] polygon, float strength)
    {
        if (Geometry2D.TriangulatePolygon(polygon).Length > 0)
            View.DrawColoredPolygon(polygon, Alpha(_color, strength * .125f));
        DrawPolyline([.. polygon, polygon[0]], Alpha(_color, strength * .8f), 1.5f, true);
    }

    private void Crescent(float t, float a)
    {
        for (int i = 0; i < Count; i++)
        {
            float y = Spread(i, Count, -21, 22);
            var center = new Vector2(-29 + t * 56, y);
            var points = ArcPoints(center, new Vector2(107, 49), -2.75f, .43f);
            var inner = ArcPoints(center + new Vector2(0, 8), new Vector2(101, 32), .43f, -2.75f);
            Facet([.. points, .. inner], a * .82f);
            Edge(points, a, i == 0 ? 3.8f : 2.5f);
        }
    }

    private void Flare(float t, float a)
    {
        var radius = 15 + 94 * t;
        for (int i = 0; i < Count + 3; i++)
        {
            var ray = Vector2.FromAngle(i * Mathf.Tau / (Count + 3));
            var at = ray * radius * (i % 2 == 0 ? 1 : .72f);
            Edge([at, at + ray * (15 + a * 23)], a * .83f, 2.2f);
        }
        Diamond(Vector2.Zero, new Vector2(11, 30) * a, Alpha(White, a));
        Ellipse(Vector2.Zero, new Vector2(39, 22) * (1 + t), 0, Mathf.Tau, Alpha(_color, a * .66f), 1.7f);
    }

    private void Leaves(float t, float a)
    {
        for (int i = 0; i < Count; i++)
        {
            float x = Spread(i, Count, -72, 72) * (.65f + t * .4f);
            float dx = Mathf.Sin(t * 3.6f + i) * 16;
            var at = new Vector2(x + dx, 61 - t * (142 + i % 3 * 12));
            float width = 17 * Mathf.Abs(Mathf.Cos(t * 3.5f + i * .7f)) + 5;
            Facet([at + new Vector2(-width, -29), at + new Vector2(width, -24),
                at + new Vector2(width, 24), at + new Vector2(-width, 29)], a);
            Diamond(at, new Vector2(4, 11), Alpha(White, a * .8f));
        }
    }

    private void Shield(float t, float a)
    {
        for (int i = Count - 1; i >= 0; i--)
        {
            var center = new Vector2(0, 7 + i * 14);
            float r = (.76f + .24f * Mathf.SmoothStep(0, .45f, t)) * (1.0f + i * .12f);
            Vector2[] outline = [new(-61,-56), new(0,-82), new(61,-56), new(53,18),
                new(36,55), new(0,76), new(-36,55), new(-53,18)];
            for (int j = 0; j < outline.Length; j++) outline[j] = center + outline[j] * r;
            Facet(outline, a * (1 - i * .16f));
            DrawPolyline([outline[7], outline[0], outline[1], outline[2], outline[3]], Alpha(White, a * .64f), 2.4f, true);
        }
        Diamond(new Vector2(0, -37), new Vector2(8, 15), Alpha(_color, a * .8f));
    }

    private void Converge(float t, float a)
    {
        for (int i = 0; i < Count; i++)
        {
            float angle = i * Mathf.Tau / Count + t * 1.6f;
            var at = Vector2.FromAngle(angle) * new Vector2(104, 78) * (1.10f - .93f * t);
            var tail = Vector2.FromAngle(angle - .18f) * (at.Length() + 23);
            Edge([tail, at], a * .70f, 1.8f);
            Diamond(at, new Vector2(4, 8), Alpha(White, a));
        }
        DrawCircle(Vector2.Zero, 9 + 19 * t, Alpha(_color, a * .18f));
    }

    private void Comet(float t, float a)
    {
        for (int i = 0; i < Count; i++)
        {
            var at = new Vector2(-94 + 172 * t, Spread(i, Count, -45, 47));
            var points = Curve(at + new Vector2(-82, 51), at + new Vector2(-51, -23), at);
            Edge(points, a * (1 - i * .13f), 3.1f);
            Diamond(at, new Vector2(12, 6), Alpha(White, a));
        }
    }

    private void Wave(float t, float a)
    {
        for (int i = 0; i < Count; i++)
        {
            var center = new Vector2(-64 + 115 * t, Spread(i, Count, -47, 48));
            var points = Curve(center + new Vector2(-48,-37), center + new Vector2(106,0), center + new Vector2(-40,39));
            Edge(points, a * (1 - .13f * i), 2.8f);
        }
    }

    private void Marks(float t, float a)
    {
        for (int i = 0; i < Count; i++)
        {
            var center = new Vector2(Spread(i, Count, -62, 62), MathF.Sin(i * 2.2f) * 32 - t * 18);
            var cut = new Vector2(24, -34);
            Diamond(center, new Vector2(12, 19), Alpha(_color, a * .18f));
            Edge([center - cut, center + new Vector2(-9,-6), center + new Vector2(4,-8),
                center + new Vector2(3,6), center + cut], a * .8f, 2);
            Ellipse(center, new Vector2(35, 24), .4f, 5.2f, Alpha(_color, a * .54f), 1.6f);
        }
    }

    private void Veil(float t, float a)
    {
        for (int i = 0; i < Count; i++)
        {
            var at = new Vector2(Spread(i, Count, -63, 63) + 25 * t, 8);
            var top = at + new Vector2(0, -79);
            var bottom = at + new Vector2(0, 70);
            var front = Curve(top, at + new Vector2(54, -14), bottom);
            var rear = Curve(bottom, at + new Vector2(-32, 34), top);
            Facet([.. front, .. rear[1..^1]], a * .54f);
            DrawPolyline(front, Alpha(_color, a * .65f), 1.7f, true);
        }
    }

    private void Tether(float t, float a)
    {
        var start = _source.Normalized() * Mathf.Min(_source.Length() * .62f, 244);
        for (int i = 0; i < Count; i++)
        {
            var at = new Vector2(Spread(i, Count, -26, 26), Spread(i, Count, -30, 22));
            var points = Curve(start, start * .46f + new Vector2(0, -83 + i * 56), at);
            DrawPolyline(points, Alpha(_color, a * .75f), 1.8f, true);
            Diamond(points[(int)((points.Length-1) * (.95f-.8f*t))], new Vector2(6, 9), Alpha(White, a));
        }
    }

    private void Rift(float t, float a)
    {
        for (int i = 0; i < Count; i++)
        {
            float x = Spread(i, Count, -73, 70);
            var points = new Vector2[7];
            for (int j = 0; j < points.Length; j++)
                points[j] = new Vector2(x + (j % 2 == 0 ? -10 : 11) + j * 9, -72 + j * 22);
            Edge(points, a * .84f, 2.2f);
            DrawLine(points[3], points[3] + new Vector2(26 + 21*t, -14), Alpha(_color, a * .68f), 1.5f, true);
        }
    }

    private void Runes(float t, float a)
    {
        var radii = new Vector2(84 + 22*t, 62 + 13*t);
        Ellipse(Vector2.Zero, radii, t, Mathf.Tau+t, Alpha(_color, a*.58f), 1.3f);
        for (int i = 0; i < Count; i++)
        {
            var point = Vector2.FromAngle(i * Mathf.Tau / Count - .9f + t*.36f) * radii;
            DrawCircle(point, 12, Alpha(_color, a*.15f));
            Ellipse(point, new Vector2(13, 13), 0, Mathf.Tau, Alpha(_color, a*.9f), 1.6f);
            Diamond(point, new Vector2(4, 7), Alpha(White, a));
        }
    }

    private void Echo(float t, float a)
    {
        for (int i = 0; i < Count; i++)
        {
            float p = (t + .22f*i) % 1f;
            var radii = new Vector2(25 + 117*p, 9 + 63*p);
            float ring = a * (1-p);
            Ellipse(Vector2.Zero, radii, -.4f, 4.95f, Alpha(_color, ring*.86f), 2.8f);
            Diamond(radii * Vector2.FromAngle(.7f + i), new Vector2(3.5f, 7), Alpha(White, ring));
        }
    }

    private void Volley(float t)
    {
        for (int i = 0; i < Count; i++)
        {
            float window = (t - Spread(i,Count,0,.64f)) / .35f;
            if (window is <= 0 or >= 1) continue;
            float a = Envelope(window);
            var at = new Vector2(Spread(i,Count,-46,46) + 49*window, MathF.Cos(i*2.4f)*38);
            Edge([at + new Vector2(-109,27), at], a, 3.6f);
            Diamond(at, new Vector2(10,7), Alpha(White,a));
        }
    }

    private void Spear(float t, float a)
    {
        for (int i = 0; i < Count; i++)
        {
            var at = new Vector2(-46 + t*88, Spread(i,Count,-51,51));
            var blade = at + new Vector2(59,0);
            Facet([at + new Vector2(-66,-8), at + new Vector2(16,-20), blade, at + new Vector2(16,19), at + new Vector2(-66,7)], a);
            Edge([at+new Vector2(-87,0),blade],a*.9f,3);
            Diamond(at + new Vector2(12,0),new Vector2(12,8),Alpha(White,a*.83f));
        }
    }

    private void Rain(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            var at = new Vector2(Spread(i,Count,-105,106), -119 + (131 + i%3*17)*t);
            Edge([at + new Vector2(11,-114), at], a*(i%2==0 ? .98f:.65f), Count==1 ? 8 : 3.2f);
            Diamond(at,new Vector2(9,15),Alpha(White,a*.9f));
        }
    }

    private void Ash(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            var at = new Vector2(Spread(i,Count,-103,103), 42-127*t);
            if (t < .55f)
                Facet([at+new Vector2(-10,-15),at+new Vector2(13,-18),at+new Vector2(16,12),at+new Vector2(-11,15)],a*(1-t));
            for (int j=0; j<5; j++)
            {
                var spark = at+Vector2.FromAngle(j*1.31f+i)*t*(29+j*6);
                Diamond(spark,new Vector2(3,6)*(1-t*.6f),Alpha(_color,a*(.49f+j*.1f)));
            }
        }
    }

    private void Prism(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            var at = Vector2.FromAngle(i*Mathf.Tau/Count + t*.78f) * new Vector2(54+45*t,58+27*t);
            var top = at+new Vector2(-6,-28);
            var left = at+new Vector2(-16,10);
            var right = at+new Vector2(19,11);
            var end = at+new Vector2(5,23);
            Facet([top,right,end,left],a);
            DrawPolyline([top,at+new Vector2(2,2),end,right],Alpha(White,a*.73f),1.5f,true);
        }
    }

    private void Bolt(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            var at = new Vector2(Spread(i,Count,-71,67), 37-t*68);
            var lines = new[] {at+new Vector2(-26,51), at+new Vector2(9,9),at+new Vector2(-9,4),at+new Vector2(22,-45)};
            Edge(lines,a*(i%2==0 ? .92f:.58f),2.7f);
        }
        Diamond(Vector2.Zero,new Vector2(11,25)*a,Alpha(_color,a*.8f));
    }

    private void Growth(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            float x = Spread(i,Count,-92,92);
            var at = new Vector2(x,47-113*t);
            var stem = Curve(new Vector2(x*.54f,79),new Vector2(x*.44f-22,22),at);
            Edge(stem,a*.75f,1.9f);
            var bud = new Vector2(8,16)*(.38f+t);
            Diamond(at,bud,Alpha(_color,a));
            Diamond(at+new Vector2(0,-4),bud*.44f,Alpha(White,a*.9f));
        }
    }

    private void Hourglass(float t, float a)
    {
        Edge([new(-51,-66),new(51,-66),new(31,-27),Vector2.Zero,new(-32,28),new(-51,66),
              new(51,66),new(33,28),Vector2.Zero,new(-34,-25),new(-51,-66)],a*.87f,2.6f);
        for (int i=0; i<5; i++)
            Diamond(new Vector2(Mathf.Sin(i*4)*6,((t+i*.18f)%1f)*100-50),new Vector2(3,6),Alpha(_color,a));
    }

    private void Gate(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            float width = 52+i*16;
            float top = -79-i*14;
            var arch = new Vector2[] {new(-width,65),new(-width,top+26),new(-width*.55f,top+7),
                new(0,top-9),new(width*.55f,top+7),new(width,top+26),new(width,65)};
            Edge(arch,a*(1-.19f*i),i==0?3.6f:1.7f);
            Diamond(arch[3],new Vector2(5,11),Alpha(_color,a*.72f));
            var gap = 20+t*36;
            DrawLine(new Vector2(-gap,top+32),new Vector2(-gap,58),Alpha(_color,a*.51f),1.6f,true);
            DrawLine(new Vector2(gap,top+32),new Vector2(gap,58),Alpha(_color,a*.51f),1.6f,true);
        }
    }

    private void Orbit(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            var radii = new Vector2(85+i*12,30+i*12);
            float theta = t*5.9f+i*2.15f;
            Ellipse(Vector2.Zero,radii,theta-2.65f,theta,Alpha(_color,a*.78f),2.2f);
            Diamond(radii*Vector2.FromAngle(theta),new Vector2(7,8),Alpha(White,a));
        }
    }

    private void Cross(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            var at = new Vector2(Spread(i,Count,-63,63), i%2*25);
            float size = Count==1 ? 87 : 59;
            Edge([at+new Vector2(-size,0),at+new Vector2(size,0)],a,2.8f);
            Edge([at+new Vector2(0,-size),at+new Vector2(0,size)],a*.84f,2.4f);
            Diamond(at,new Vector2(13,29)*a,Alpha(White,a));
        }
    }

    private void Cleanse(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            var at = new Vector2(Spread(i,Count,-83,83), -100 + t*196 + i%2*21);
            var stripe = Curve(at+new Vector2(-24,-47),at+new Vector2(7,-12),at);
            Edge(stripe,a*.81f,1.8f);
            DrawCircle(at+new Vector2(11,8),7,Alpha(_color,a*.16f));
        }
        Ellipse(new Vector2(0,68),new Vector2(28+104*t,14+24*t),0,Mathf.Tau,Alpha(_color,a*.56f),2);
    }

    private void Crown(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            var ray = Vector2.FromAngle(Spread(i,Count,-2.81f,-.33f));
            var at = ray * new Vector2(95,105) * (.82f + t*.35f);
            Edge([at*.55f,at,at+ray*17],a*.84f,2.4f);
            Diamond(at,new Vector2(8,16),Alpha(_color,a));
        }
        Ellipse(new Vector2(0,-11),new Vector2(93,36), .03f,3.06f,Alpha(_color,a*.76f),2.7f);
        Diamond(new Vector2(0,-33),new Vector2(11,28),Alpha(White,a*.85f));
    }

    private void Bloom(float t, float a)
    {
        for (int i=0; i<Count; i++)
        {
            float angle = i*Mathf.Tau/Count+t*.65f;
            var direction = Vector2.FromAngle(angle);
            var side = direction.Rotated(Mathf.Pi/2);
            var tip = direction * (44+69*t);
            var first = Curve(direction*13,tip*.49f+side*33,tip);
            var last = Curve(tip,tip*.47f-side*31,direction*13);
            Facet([.. first,.. last[1..^1]],a*.77f);
            DrawPolyline(first,Alpha(White,a*.70f),1.8f,true);
        }
        Diamond(Vector2.Zero,new Vector2(12,23)*a,Alpha(_color,a));
    }

    private void Singularity(float t, float a)
    {
        float darkRadius = 19+12*t;
        DrawCircle(Vector2.Zero,darkRadius, new Color(.035f,.050f,.075f,a*.80f));
        for (int i=0; i<Count; i++)
        {
            float start=i*Mathf.Tau/Count+t*2.7f;
            var points = new Vector2[24];
            for (int j=0; j<points.Length; j++)
            {
                float p=j/(float)(points.Length-1);
                points[j]=Vector2.FromAngle(start+p*1.61f)*Mathf.Lerp(110-39*t,darkRadius+4,p);
            }
            Edge(points,a*.76f,2.1f);
        }
        Ellipse(Vector2.Zero,Vector2.One*darkRadius,0,Mathf.Tau,Alpha(_color,a),1.6f);
    }

    private static Vector2[] Curve(Vector2 from, Vector2 control, Vector2 to)
    {
        var points = new Vector2[15];
        for (int i=0; i<points.Length; i++)
        {
            float t=i/(float)(points.Length-1);
            points[i]=from.Lerp(control,t).Lerp(control.Lerp(to,t),t);
        }
        return points;
    }

    private static Vector2[] ArcPoints(Vector2 center, Vector2 radii, float start, float end)
    {
        var points = new Vector2[22];
        for (int i=0; i<points.Length; i++)
            points[i]=center+radii*Vector2.FromAngle(Mathf.Lerp(start,end,i/(float)(points.Length-1)));
        return points;
    }
}
