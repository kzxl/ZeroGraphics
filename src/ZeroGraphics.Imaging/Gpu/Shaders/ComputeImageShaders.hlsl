// =========================================================================
// ZeroGraphics DirectCompute 5.0 Image Kernels
// High-performance compute shaders with LDS shared memory caching
// =========================================================================

// -------------------------------------------------------------------------
// 1. Color Adjust & Fused Point Operations (CS_ColorAdjust)
// -------------------------------------------------------------------------
Texture2D<float4> ColorInput : register(t0);
RWTexture2D<float4> ColorOutput : register(u0);

cbuffer ColorComputeBuffer : register(b0)
{
    float Brightness;   // -1.0 to 1.0 (default 0.0)
    float Contrast;     // 0.0 to 3.0 (default 1.0)
    float Grayscale;    // 1.0 = true, 0.0 = false
    float Invert;       // 1.0 = true, 0.0 = false
    float Gamma;        // default 1.0
    float Threshold;    // < 0 = disabled, >= 0 = binary cutoff
    float2 Padding;
};

[numthreads(16, 16, 1)]
void CS_ColorAdjust(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint width, height;
    ColorOutput.GetDimensions(width, height);
    if (dispatchThreadId.x >= width || dispatchThreadId.y >= height)
        return;

    float4 color = ColorInput.Load(int3(dispatchThreadId.xy, 0));

    if (Grayscale > 0.5f)
    {
        float gray = dot(color.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        color.rgb = float3(gray, gray, gray);
    }

    color.rgb = (color.rgb - 0.5f) * Contrast + 0.5f + Brightness;

    if (Invert > 0.5f)
    {
        color.rgb = 1.0f - color.rgb;
    }

    if (Gamma > 0.01f && abs(Gamma - 1.0f) > 0.001f)
    {
        color.rgb = pow(max(color.rgb, 0.00001f), 1.0f / Gamma);
    }

    if (Threshold >= 0.0f)
    {
        float luma = dot(color.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        float bin = luma >= Threshold ? 1.0f : 0.0f;
        color.rgb = float3(bin, bin, bin);
    }

    ColorOutput[dispatchThreadId.xy] = saturate(color);
}

// -------------------------------------------------------------------------
// 2. Separable Gaussian Blur (Horizontal Pass with LDS)
// -------------------------------------------------------------------------
Texture2D<float4> BlurInputH : register(t0);
RWTexture2D<float4> BlurOutputH : register(u0);

cbuffer BlurParamsH : register(b0)
{
    uint ImageWidthH;
    uint ImageHeightH;
    int BlurRadiusH;     // 1 to 7
    float BlurPadH;
    float4 BlurWeightsH[2]; // Packed weights: [0]=w0,w1,w2,w3, [1]=w4,w5,w6,w7
};

#define BLUR_BLOCK_X 128
#define MAX_RADIUS 7
#define LDS_SIZE_X (BLUR_BLOCK_X + 2 * MAX_RADIUS)

groupshared float4 s_Row[LDS_SIZE_X];

[numthreads(BLUR_BLOCK_X, 1, 1)]
void CS_BlurHorizontal(uint3 groupThreadId : SV_GroupThreadID,
                       uint3 groupId : SV_GroupID)
{
    int tid = (int)groupThreadId.x;
    int baseX = (int)groupId.x * BLUR_BLOCK_X;
    int y = (int)groupId.y;

    if (y >= (int)ImageHeightH) return;

    int radius = clamp(BlurRadiusH, 1, MAX_RADIUS);

    // 1. Center element
    int globalX = baseX + tid;
    s_Row[tid + radius] = BlurInputH.Load(int3(clamp(globalX, 0, (int)ImageWidthH - 1), y, 0));

    // 2. Left apron
    if (tid < radius)
    {
        int leftX = baseX - radius + tid;
        s_Row[tid] = BlurInputH.Load(int3(clamp(leftX, 0, (int)ImageWidthH - 1), y, 0));
    }

    // 3. Right apron
    if (tid < radius)
    {
        int rightX = baseX + BLUR_BLOCK_X + tid;
        s_Row[BLUR_BLOCK_X + radius + tid] = BlurInputH.Load(int3(clamp(rightX, 0, (int)ImageWidthH - 1), y, 0));
    }

    GroupMemoryBarrierWithGroupSync();

    if (globalX < (int)ImageWidthH)
    {
        float w[8];
        w[0] = BlurWeightsH[0].x;
        w[1] = BlurWeightsH[0].y;
        w[2] = BlurWeightsH[0].z;
        w[3] = BlurWeightsH[0].w;
        w[4] = BlurWeightsH[1].x;
        w[5] = BlurWeightsH[1].y;
        w[6] = BlurWeightsH[1].z;
        w[7] = BlurWeightsH[1].w;

        int centerIdx = tid + radius;
        float4 sum = s_Row[centerIdx] * w[0];
        [unroll]
        for (int k = 1; k <= MAX_RADIUS; k++)
        {
            if (k <= radius)
            {
                sum += (s_Row[centerIdx - k] + s_Row[centerIdx + k]) * w[k];
            }
        }
        BlurOutputH[int2(globalX, y)] = sum;
    }
}

// -------------------------------------------------------------------------
// 3. Separable Gaussian Blur (Vertical Pass with LDS)
// -------------------------------------------------------------------------
Texture2D<float4> BlurInputV : register(t0);
RWTexture2D<float4> BlurOutputV : register(u0);

cbuffer BlurParamsV : register(b0)
{
    uint ImageWidthV;
    uint ImageHeightV;
    int BlurRadiusV;     // 1 to 7
    float BlurPadV;
    float4 BlurWeightsV[2]; // Packed weights: [0]=w0,w1,w2,w3, [1]=w4,w5,w6,w7
};

#define BLUR_BLOCK_Y 128
#define LDS_SIZE_Y (BLUR_BLOCK_Y + 2 * MAX_RADIUS)

groupshared float4 s_Col[LDS_SIZE_Y];

[numthreads(1, BLUR_BLOCK_Y, 1)]
void CS_BlurVertical(uint3 groupThreadId : SV_GroupThreadID,
                     uint3 groupId : SV_GroupID)
{
    int tid = (int)groupThreadId.y;
    int x = (int)groupId.x;
    int baseY = (int)groupId.y * BLUR_BLOCK_Y;

    if (x >= (int)ImageWidthV) return;

    int radius = clamp(BlurRadiusV, 1, MAX_RADIUS);

    // 1. Center element
    int globalY = baseY + tid;
    s_Col[tid + radius] = BlurInputV.Load(int3(x, clamp(globalY, 0, (int)ImageHeightV - 1), 0));

    // 2. Top apron
    if (tid < radius)
    {
        int topY = baseY - radius + tid;
        s_Col[tid] = BlurInputV.Load(int3(x, clamp(topY, 0, (int)ImageHeightV - 1), 0));
    }

    // 3. Bottom apron
    if (tid < radius)
    {
        int bottomY = baseY + BLUR_BLOCK_Y + tid;
        s_Col[BLUR_BLOCK_Y + radius + tid] = BlurInputV.Load(int3(x, clamp(bottomY, 0, (int)ImageHeightV - 1), 0));
    }

    GroupMemoryBarrierWithGroupSync();

    if (globalY < (int)ImageHeightV)
    {
        float w[8];
        w[0] = BlurWeightsV[0].x;
        w[1] = BlurWeightsV[0].y;
        w[2] = BlurWeightsV[0].z;
        w[3] = BlurWeightsV[0].w;
        w[4] = BlurWeightsV[1].x;
        w[5] = BlurWeightsV[1].y;
        w[6] = BlurWeightsV[1].z;
        w[7] = BlurWeightsV[1].w;

        int centerIdx = tid + radius;
        float4 sum = s_Col[centerIdx] * w[0];
        [unroll]
        for (int k = 1; k <= MAX_RADIUS; k++)
        {
            if (k <= radius)
            {
                sum += (s_Col[centerIdx - k] + s_Col[centerIdx + k]) * w[k];
            }
        }
        BlurOutputV[int2(x, globalY)] = sum;
    }
}

// -------------------------------------------------------------------------
// 4. Sobel Gradient & 3x3 Sharpening with 18x18 LDS Apron
// -------------------------------------------------------------------------
Texture2D<float4> ConvInput : register(t0);
RWTexture2D<float4> ConvOutput : register(u0);

cbuffer ConvParams : register(b0)
{
    uint ConvWidth;
    uint ConvHeight;
    uint ConvMode;       // 0 = Sobel Magnitude, 1 = Laplacian Sharpen
    float ConvStrength;  // Multiplier / Sharpen strength
};

groupshared float4 s_Tile[18][18];

[numthreads(16, 16, 1)]
void CS_Convolution3x3(uint3 groupThreadId : SV_GroupThreadID,
                       uint3 groupId : SV_GroupID)
{
    int tx = (int)groupThreadId.x;
    int ty = (int)groupThreadId.y;
    int gx = (int)groupId.x * 16 + tx;
    int gy = (int)groupId.y * 16 + ty;

    int maxW = (int)ConvWidth - 1;
    int maxH = (int)ConvHeight - 1;

    // Center pixel
    s_Tile[ty + 1][tx + 1] = ConvInput.Load(int3(clamp(gx, 0, maxW), clamp(gy, 0, maxH), 0));

    // Apron borders
    if (tx == 0)
        s_Tile[ty + 1][0] = ConvInput.Load(int3(clamp(gx - 1, 0, maxW), clamp(gy, 0, maxH), 0));
    if (tx == 15)
        s_Tile[ty + 1][17] = ConvInput.Load(int3(clamp(gx + 1, 0, maxW), clamp(gy, 0, maxH), 0));
    if (ty == 0)
        s_Tile[0][tx + 1] = ConvInput.Load(int3(clamp(gx, 0, maxW), clamp(gy - 1, 0, maxH), 0));
    if (ty == 15)
        s_Tile[17][tx + 1] = ConvInput.Load(int3(clamp(gx, 0, maxW), clamp(gy + 1, 0, maxH), 0));

    // Apron corners
    if (tx == 0 && ty == 0)
        s_Tile[0][0] = ConvInput.Load(int3(clamp(gx - 1, 0, maxW), clamp(gy - 1, 0, maxH), 0));
    if (tx == 15 && ty == 0)
        s_Tile[0][17] = ConvInput.Load(int3(clamp(gx + 1, 0, maxW), clamp(gy - 1, 0, maxH), 0));
    if (tx == 0 && ty == 15)
        s_Tile[17][0] = ConvInput.Load(int3(clamp(gx - 1, 0, maxW), clamp(gy + 1, 0, maxH), 0));
    if (tx == 15 && ty == 15)
        s_Tile[17][17] = ConvInput.Load(int3(clamp(gx + 1, 0, maxW), clamp(gy + 1, 0, maxH), 0));

    GroupMemoryBarrierWithGroupSync();

    if (gx < (int)ConvWidth && gy < (int)ConvHeight)
    {
        int py = ty + 1;
        int px = tx + 1;

        float4 c = s_Tile[py][px];

        if (ConvMode == 0) // Sobel
        {
            float tl = dot(s_Tile[py - 1][px - 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float tc = dot(s_Tile[py - 1][px    ].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float tr = dot(s_Tile[py - 1][px + 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float ml = dot(s_Tile[py    ][px - 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float mr = dot(s_Tile[py    ][px + 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float bl = dot(s_Tile[py + 1][px - 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float bc = dot(s_Tile[py + 1][px    ].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float br = dot(s_Tile[py + 1][px + 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));

            float gx_val = (-tl + tr) + 2.0f * (-ml + mr) + (-bl + br);
            float gy_val = (-tl - 2.0f * tc - tr) + (bl + 2.0f * bc + br);
            float mag = sqrt(gx_val * gx_val + gy_val * gy_val) * ConvStrength;
            float val = saturate(mag);
            ConvOutput[int2(gx, gy)] = float4(val, val, val, c.a);
        }
        else // Sharpen
        {
            float4 t = s_Tile[py - 1][px];
            float4 b = s_Tile[py + 1][px];
            float4 l = s_Tile[py][px - 1];
            float4 r = s_Tile[py][px + 1];

            float4 laplacian = 5.0f * c - (t + b + l + r);
            float4 sharpened = lerp(c, laplacian, ConvStrength);
            ConvOutput[int2(gx, gy)] = saturate(sharpened);
        }
    }
}
