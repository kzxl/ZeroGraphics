// =========================================================================
// ZeroGraphics GPU Image Pipeline Shaders (Direct3D 11 / Shader Model 4.0)
// High-performance image kernels & Operation Fusion execution
// =========================================================================

struct VS_OUTPUT {
    float4 Pos : SV_POSITION;
    float2 Tex : TEXCOORD0;
};

// -------------------------------------------------------------------------
// 1. Full-screen Triangle (No Vertex Buffer Required)
// -------------------------------------------------------------------------
VS_OUTPUT VS_Fullscreen(uint id : SV_VertexID) {
    VS_OUTPUT output;
    output.Tex = float2((id << 1) & 2, id & 2);
    output.Pos = float4(output.Tex * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f), 0.0f, 1.0f);
    return output;
}

Texture2D inputTexture : register(t0);
SamplerState defaultSampler : register(s0);

// -------------------------------------------------------------------------
// 2. Texture Copy / Resampling (Bilinear/Point Resize)
// -------------------------------------------------------------------------
float4 PS_Resize(VS_OUTPUT input) : SV_TARGET {
    return inputTexture.Sample(defaultSampler, input.Tex);
}

// -------------------------------------------------------------------------
// 3. Color Transformation (Brightness, Contrast, BT.709 Grayscale, Invert)
// -------------------------------------------------------------------------
cbuffer ColorBuffer : register(b0) {
    float Brightness;   // -1.0 to 1.0 (default 0.0)
    float Contrast;     // 0.0 to 3.0 (default 1.0)
    float Grayscale;    // 1.0 = true, 0.0 = false
    float Invert;       // 1.0 = true, 0.0 = false
};

float4 PS_ColorAdjust(VS_OUTPUT input) : SV_TARGET {
    float4 c = inputTexture.Sample(defaultSampler, input.Tex);
    if (Grayscale > 0.5f) {
        float gray = dot(c.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        c.rgb = float3(gray, gray, gray);
    }
    c.rgb = (c.rgb - 0.5f) * Contrast + 0.5f + Brightness;
    if (Invert > 0.5f) {
        c.rgb = 1.0f - c.rgb;
    }
    return saturate(c);
}

// -------------------------------------------------------------------------
// 4. Separable Gaussian Blur (1D Horizontal / Vertical 9-Tap)
// -------------------------------------------------------------------------
cbuffer BlurBuffer : register(b0) {
    float2 TexelSize;       // (1.0 / Width, 1.0 / Height)
    float2 BlurDirection;   // (1, 0) for H, (0, 1) for V
};

static const float blurWeights[5] = { 0.227027f, 0.1945946f, 0.1216216f, 0.054054f, 0.016216f };

float4 PS_GaussianBlur(VS_OUTPUT input) : SV_TARGET {
    float2 offset = TexelSize * BlurDirection;
    float4 result = inputTexture.Sample(defaultSampler, input.Tex) * blurWeights[0];
    [unroll]
    for (int i = 1; i < 5; i++) {
        result += inputTexture.Sample(defaultSampler, input.Tex + offset * (float)i) * blurWeights[i];
        result += inputTexture.Sample(defaultSampler, input.Tex - offset * (float)i) * blurWeights[i];
    }
    return result;
}

// -------------------------------------------------------------------------
// 5. Sobel 3x3 Gradient Edge Detection
// -------------------------------------------------------------------------
cbuffer SobelBuffer : register(b0) {
    float2 SobelTexelSize;
    float Multiplier;       // default 1.0f
    float SobelThreshold;   // 0.0 = grayscale edges, >0.0 = binarized edges
};

float4 PS_Sobel(VS_OUTPUT input) : SV_TARGET {
    float2 uv = input.Tex;
    float dx = SobelTexelSize.x;
    float dy = SobelTexelSize.y;

    float tl = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float tc = dot(inputTexture.Sample(defaultSampler, uv + float2(  0, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float tr = dot(inputTexture.Sample(defaultSampler, uv + float2( dx, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float ml = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx,   0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float mr = dot(inputTexture.Sample(defaultSampler, uv + float2( dx,   0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float bl = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx,  dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float bc = dot(inputTexture.Sample(defaultSampler, uv + float2(  0,  dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float br = dot(inputTexture.Sample(defaultSampler, uv + float2( dx,  dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));

    float gx = (-1.0f * tl + 1.0f * tr) + (-2.0f * ml + 2.0f * mr) + (-1.0f * bl + 1.0f * br);
    float gy = (-1.0f * tl - 2.0f * tc - 1.0f * tr) + (1.0f * bl + 2.0f * bc + 1.0f * br);

    float mag = (abs(gx) + abs(gy)) * Multiplier;
    if (SobelThreshold > 0.0f) {
        mag = (mag >= SobelThreshold) ? 1.0f : 0.0f;
    }
    mag = saturate(mag);
    return float4(mag, mag, mag, 1.0f);
}

// -------------------------------------------------------------------------
// 6. Laplacian 3x3 Sharpening
// -------------------------------------------------------------------------
cbuffer SharpenBuffer : register(b0) {
    float2 SharpenTexelSize;
    float SharpenStrength;  // default 1.0f
    float SharpenPadding;
};

float4 PS_Sharpen(VS_OUTPUT input) : SV_TARGET {
    float2 uv = input.Tex;
    float dx = SharpenTexelSize.x;
    float dy = SharpenTexelSize.y;

    float4 center = inputTexture.Sample(defaultSampler, uv);
    float4 top    = inputTexture.Sample(defaultSampler, uv + float2(0, -dy));
    float4 bottom = inputTexture.Sample(defaultSampler, uv + float2(0, dy));
    float4 left   = inputTexture.Sample(defaultSampler, uv + float2(-dx, 0));
    float4 right  = inputTexture.Sample(defaultSampler, uv + float2(dx, 0));

    float4 result = center * (1.0f + 4.0f * SharpenStrength) - (top + bottom + left + right) * SharpenStrength;
    result.a = center.a;
    return saturate(result);
}

// -------------------------------------------------------------------------
// 7. Binary Thresholding
// -------------------------------------------------------------------------
cbuffer ThresholdBuffer : register(b0) {
    float Cutoff;           // 0.0 to 1.0
    float ThresholdInvert;  // 1.0 = true, 0.0 = false
    float2 ThresholdPadding;
};

float4 PS_Threshold(VS_OUTPUT input) : SV_TARGET {
    float4 c = inputTexture.Sample(defaultSampler, input.Tex);
    float lum = dot(c.rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float val = (lum >= Cutoff) ? 1.0f : 0.0f;
    if (ThresholdInvert > 0.5f) {
        val = 1.0f - val;
    }
    return float4(val, val, val, 1.0f);
}

// -------------------------------------------------------------------------
// 8. Operation Fusion (Multi-step single-pass pipeline)
// -------------------------------------------------------------------------
cbuffer FusedBuffer : register(b0) {
    float2 FusedTexelSize;
    float FusedBrightness;
    float FusedContrast;

    float FusedGrayscale;
    float FusedInvert;
    float FusedSharpenStrength;
    float FusedThresholdCutoff; // < 0 means threshold disabled
};

float4 PS_Fused(VS_OUTPUT input) : SV_TARGET {
    float2 uv = input.Tex;
    float4 c;

    if (FusedSharpenStrength > 0.0f) {
        float dx = FusedTexelSize.x;
        float dy = FusedTexelSize.y;
        float4 center = inputTexture.Sample(defaultSampler, uv);
        float4 top    = inputTexture.Sample(defaultSampler, uv + float2(0, -dy));
        float4 bottom = inputTexture.Sample(defaultSampler, uv + float2(0, dy));
        float4 left   = inputTexture.Sample(defaultSampler, uv + float2(-dx, 0));
        float4 right  = inputTexture.Sample(defaultSampler, uv + float2(dx, 0));
        c = center * (1.0f + 4.0f * FusedSharpenStrength) - (top + bottom + left + right) * FusedSharpenStrength;
        c.a = center.a;
    } else {
        c = inputTexture.Sample(defaultSampler, uv);
    }

    if (FusedGrayscale > 0.5f) {
        float gray = dot(c.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        c.rgb = float3(gray, gray, gray);
    }

    c.rgb = (c.rgb - 0.5f) * FusedContrast + 0.5f + FusedBrightness;

    if (FusedInvert > 0.5f) {
        c.rgb = 1.0f - c.rgb;
    }

    c = saturate(c);

    if (FusedThresholdCutoff >= 0.0f) {
        float lum = dot(c.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        float val = (lum >= FusedThresholdCutoff) ? 1.0f : 0.0f;
        c = float4(val, val, val, 1.0f);
    }

    return c;
}
