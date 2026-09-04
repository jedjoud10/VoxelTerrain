using jedjoud.VoxelTerrain.Generation;
using jedjoud.VoxelTerrain.Props;
using Unity.Mathematics;
using Random = jedjoud.VoxelTerrain.Generation.Random;

public class TestTerrain1 : ManagedTerrainGraph {
    public Inject<float> scale;
    public Inject<float> amp;
    public Inject<float> amp2;
    public Inject<float> warpScale;
    public Inject<float> warpAmp;
    public Inject<float> normalStrength;
    public Inject<float> bias;
    public Inject<float> heightBias;
    public Inject<float> cellularOffset;
    public Inject<float> smooth;
    public Inject<float> smooth2;
    public Inject<float> probability;
    public Inject<float> angScale;
    public Inject<float> angAmplitude;
    public Sdf.DistanceMetric metric;

    public override void Density(in Variable<float3> position, out Variable<float> density) {
        var h = Noise.VoronoiF1(position, scale, amp).Min(0, smooth);
        var ang = Noise.Simplex(position.xz, angScale, angAmplitude);
        var w = new Warper<float2>(new Voronoi<float2>(warpScale, warpAmp, VoronoiType.F2)).Warpinate(position.xz);
        var c = Cellular<float2>.Simple(metric, probability, offset: cellularOffset).Tile(w.Scaled(scale) + (ang.x * position.y).xx) * amp2;
        density = Sdf.Union(h, c, smooth2) + position.y + heightBias;
    }

    public override void Layers(in Variable<float3> position, in Variable<float3> normal, in Variable<float> density, out Variable<float4> layers) {
        layers = Variable<float4>.New((normal.y * normalStrength + bias).Saturate().OneMinus());
    }

    public override void Props(in PropInput input, PropContext propContext) {
        PropContext.PossibleSurface surface = propContext.IsSurfaceAlongAxis(input.position, PropContext.Axis.Y);
        GenerationProp prop = new GenerationProp {
            position = surface.hitPosition,
            rotation = quaternion.identity,
            scale = 1.0f,
            variant = 0,
        };
        propContext.DeclarePropSpawn(0, surface.hit & Random.Uniform(input.position, 0.2f), prop);
    }
}