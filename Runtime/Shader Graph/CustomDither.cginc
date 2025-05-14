#ifndef CUSTOM_DITHER_INCLUDED
#define CUSTOM_DITHER_INCLUDED

void SymmetricDither_float(
    float2 ScreenPosition,
    float DitherThreshold,
    out float DitherResult
){
    // Get screen-relative pixel position (0-1)
    float2 pixelPos = floor(ScreenPosition * _ScreenParams.xy);
    
    // Generate a pseudo-random dither pattern (Bayer 4x4 for smooth transitions)
    const float4x4 bayerMatrix = {
        0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
        12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
        3.0/16.0,  11.0/16.0, 1.0/16.0,  9.0/16.0,
        15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
    };
    
    // Calculate Bayer matrix index
    int2 bayerCoord = int2(pixelPos) % 4;
    float bayerValue = bayerMatrix[bayerCoord.x][bayerCoord.y];
    
    DitherResult = (bayerValue < DitherThreshold);
}

#endif