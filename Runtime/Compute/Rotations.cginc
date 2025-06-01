// I LOVE VIBE CODING!!!!
float4 LookAt(float3 forwardDir, float3 upDir, float rollAngle) {
    // Normalize input vectors
    forwardDir = normalize(forwardDir);
    upDir = normalize(upDir);
    rollAngle = radians(rollAngle);

    // --- Edge Case: Forward and Up are parallel ---
    // If they're nearly colinear, force a different up vector
    if (abs(dot(forwardDir, upDir)) > 0.9999)
    {
        // Try world-up (Y-axis) first
        upDir = float3(0, 1, 0);
        if (abs(dot(forwardDir, upDir)) > 0.9999)
        {
            // Fall back to Z-axis if Y fails
            upDir = float3(0, 0, 1);
        }
    }

    // --- Construct Orthonormal Basis ---
    float3 right = normalize(cross(upDir, forwardDir));
    float3 up = cross(forwardDir, right); // Ensures orthogonality

    // --- Convert Basis to Quaternion ---
    float trace = right.x + up.y + forwardDir.z;
    float4 q;

    if (trace > 0.0)
    {
        float s = sqrt(trace + 1.0) * 0.5;
        q.w = s;
        q.x = (up.z - forwardDir.y) / (4.0 * s);
        q.y = (forwardDir.x - right.z) / (4.0 * s);
        q.z = (right.y - up.x) / (4.0 * s);
    }
    else if (right.x > up.y && right.x > forwardDir.z)
    {
        float s = sqrt(1.0 + right.x - up.y - forwardDir.z) * 2.0;
        q.w = (up.z - forwardDir.y) / s;
        q.x = 0.25 * s;
        q.y = (up.x + right.y) / s;
        q.z = (forwardDir.x + right.z) / s;
    }
    else if (up.y > forwardDir.z)
    {
        float s = sqrt(1.0 + up.y - right.x - forwardDir.z) * 2.0;
        q.w = (forwardDir.x - right.z) / s;
        q.x = (up.x + right.y) / s;
        q.y = 0.25 * s;
        q.z = (forwardDir.y + up.z) / s;
    }
    else
    {
        float s = sqrt(1.0 + forwardDir.z - right.x - up.y) * 2.0;
        q.w = (right.y - up.x) / s;
        q.x = (forwardDir.x + right.z) / s;
        q.y = (forwardDir.y + up.z) / s;
        q.z = 0.25 * s;
    }

    // --- Apply Roll Rotation (if needed) ---
    if (abs(rollAngle) > 0.0001)
    {
        float halfRoll = rollAngle * 0.5;
        float sinRoll = sin(halfRoll);
        float cosRoll = cos(halfRoll);

        // Roll quaternion rotates around the forward axis
        float4 rollQuat = float4(
            forwardDir.x * sinRoll,
            forwardDir.y * sinRoll,
            forwardDir.z * sinRoll,
            cosRoll
        );

        // Combine rotations: rollQuat * q (roll happens after look-at)
        q = float4(
            rollQuat.w * q.x + rollQuat.x * q.w + rollQuat.y * q.z - rollQuat.z * q.y,
            rollQuat.w * q.y - rollQuat.x * q.z + rollQuat.y * q.w + rollQuat.z * q.x,
            rollQuat.w * q.z + rollQuat.x * q.y - rollQuat.y * q.x + rollQuat.z * q.w,
            rollQuat.w * q.w - rollQuat.x * q.x - rollQuat.y * q.y - rollQuat.z * q.z
        );
    }

    return normalize(q);
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