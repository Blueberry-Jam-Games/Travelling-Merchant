#include "/Includes/FastNoiseLite.hlsl"

fnl_state CreateTerrainNoise(int seed)
{
    fnl_state state = fnlCreateState(seed);

    state.noise_type = FNL_NOISE_OPENSIMPLEX2;
    state.fractal_type = FNL_FRACTAL_FBM;

    state.frequency = 0.00001;
    state.octaves = 3;
    state.lacunarity = 2.0;
    state.gain = 3;

    return state;
}

static float worldHeight = 3000;

float GetTerrainHeight(float2 worldXZ, int seed)
{
    fnl_state noise = CreateTerrainNoise(seed);

    float n = fnlGetNoise2D(noise, worldXZ.x, worldXZ.y);

    // Convert [-1,1] → [0,1]
    n = n * 0.5;

    return n * worldHeight;
}