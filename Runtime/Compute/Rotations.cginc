// I LOVE VIBE CODING!!!!
float4 LookAt(float3 a, float3 b) {
	a = normalize(a);
    b = normalize(b);

	// we need this desu....
	a.z *= -1;
    a.x *= -1;

    float dotProd = dot(a, b);
    float3 crossProd = cross(a, b);

    // Handle special case: 180 degree rotation (opposite vectors)
    if (dotProd < -0.9999f) {
        // Pick an arbitrary axis perpendicular to 'a'
        float3 axis = cross(a, float3(1, 0, 0));
        if (length(axis) < 0.0001f) // If 'a' is parallel to X, pick Y
            axis = cross(a, float3(0, 1, 0));
        axis = normalize(axis);
        return float4(axis.x, axis.y, axis.z, 0); // 180 degree rotation
    }

    float s = sqrt((1.0f + dotProd) * 2.0f);
    float invS = 1.0f / s;

    return float4(
        crossProd.x * invS,
        crossProd.y * invS,
        crossProd.z * invS,
        s * 0.5f
    );
}

// VIBE CODING!!!!
float4 Slerp(float4 q1, float4 q2, float t) {
    // Compute the cosine of the angle between the quaternions
    float d = dot(q1, q2);

    // If the dot product is negative, slerp won't take
    // the shorter path. Fix by reversing one quaternion.
    if (d < 0.0f)
    {
        q2 = -q2;
        d = -d;
    }

    const float DOT_THRESHOLD = 0.9995f;
    if (d > DOT_THRESHOLD)
    {
        // If the quaternions are very close, use linear interpolation
        float4 result = q1 + t * (q2 - q1);
        return normalize(result);
    }

    // Since dot is in range [0, DOT_THRESHOLD], acos is safe
    float theta_0 = acos(d);      // Angle between input quaternions
    float theta = theta_0 * t;      // Angle for interpolation

    float sin_theta = sin(theta);
    float sin_theta_0 = sin(theta_0);

    float s0 = cos(theta) - d * sin_theta / sin_theta_0;
    float s1 = sin_theta / sin_theta_0;

    return s0 * q1 + s1 * q2;
}