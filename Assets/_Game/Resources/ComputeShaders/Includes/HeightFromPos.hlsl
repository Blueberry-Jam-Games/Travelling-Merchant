#include "/Includes/FastNoiseLite.hlsl"

fnl_state CreateTerrainNoise(int seed)
{
    fnl_state state = fnlCreateState(seed);

    state.noise_type = FNL_NOISE_OPENSIMPLEX2;
    state.fractal_type = FNL_FRACTAL_FBM;

    state.frequency = 0.00001;
    state.octaves = 7;
    state.lacunarity = 2.78;
    state.gain = 0.53;
    state.seed = seed;

    return state;
}

static float worldHeight = 3000;

float GetTerrainHeight(float2 worldXZ, int seed)
{
    worldXZ += float2(0.5, 0.5);
    fnl_state noise = CreateTerrainNoise(seed);

    float n = fnlGetNoise2D(noise, worldXZ.x, worldXZ.y);

    // Convert [-1,1] → [0,1]
    n = n * 0.5;

    return n * worldHeight;
}

void GetTerrainHeightAndGradient(float2 worldXZ, int seed, out float height, out float2 gradientXZ)
{
    worldXZ += float2(0.5, 0.5);

    fnl_state noise = CreateTerrainNoise(seed);

    fnl_noise2d_gradient sample = fnlGetNoise2DWithGradient(noise, worldXZ.x, worldXZ.y);

    float heightScale = 0.5f * worldHeight;

    height = sample.value * heightScale;

    gradientXZ = sample.gradient * heightScale;
}