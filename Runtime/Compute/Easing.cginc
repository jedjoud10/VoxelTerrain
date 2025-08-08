static const float ConstantA = 1.70158;
static const float ConstantB = ConstantA * 1.525;
static const float ConstantC = ConstantA + 1.0;
static const float ConstantD = 2.0 * 3.14159265359 / 3.0;
static const float ConstantE = 2.0 * 3.14159265359 / 4.5;
static const float ConstantF = 7.5625;
static const float ConstantG = 2.75;

float Linear(float t) {
    return t;
}

float SineIn(float t) {
    return 1.0 - cos((t * 3.14159265359) / 2.0);
}

float SineOut(float t) {
    return sin((t * 3.14159265359) / 2.0);
}

float SineInOut(float t) {
    return -(cos(3.14159265359 * t) - 1.0) / 2.0;
}

float QuadIn(float t) {
    return t * t;
}

float QuadOut(float t) {
    return 1.0 - (1.0 - t) * (1.0 - t);
}

float QuadInOut(float t) {
    return t < 0.5 ? 2.0 * t * t : 1.0 - pow(-2.0 * t + 2.0, 2.0) / 2.0;
}

float CubicIn(float t) {
    return t * t * t;
}

float CubicOut(float t) {
    return 1.0 - pow(1.0 - t, 3.0);
}

float CubicInOut(float t) {
    return t < 0.5 ? 4.0 * t * t * t : 1.0 - pow(-2.0 * t + 2.0, 3.0) / 2.0;
}

float QuartIn(float t) {
    return t * t * t * t;
}

float QuartOut(float t) {
    return 1.0 - pow(1.0 - t, 4.0);
}

float QuartInOut(float t) {
    return t < 0.5 ? 8.0 * t * t * t * t : 1.0 - pow(-2.0 * t + 2.0, 4.0) / 2.0;
}

float QuintIn(float t) {
    return t * t * t * t * t;
}

float QuintOut(float t) {
    return 1.0 - pow(1.0 - t, 5.0);
}

float QuintInOut(float t) {
    return t < 0.5 ? 16.0 * t * t * t * t * t : 1.0 - pow(-2.0 * t + 2.0, 5.0) / 2.0;
}

float ExpoIn(float t) {
    return t == 0.0 ? 0.0 : pow(2.0, 10.0 * t - 10.0);
}

float ExpoOut(float t) {
    return t == 1.0 ? 1.0 : 1.0 - pow(2.0, -10.0 * t);
}

float ExpoInOut(float t) {
    return t == 0.0 ? 0.0 : t == 1.0 ? 1.0 :
           t < 0.5 ? pow(2.0, 20.0 * t - 10.0) / 2.0 :
           (2.0 - pow(2.0, -20.0 * t + 10.0)) / 2.0;
}

float CircIn(float t) {
    return 1.0 - sqrt(1.0 - t * t);
}

float CircOut(float t) {
    return sqrt(1.0 - pow(t - 1.0, 2.0));
}

float CircInOut(float t) {
    return t < 0.5 ?
        (1.0 - sqrt(1.0 - pow(2.0 * t, 2.0))) / 2.0 :
        (sqrt(1.0 - pow(-2.0 * t + 2.0, 2.0)) + 1.0) / 2.0;
}

float BackIn(float t) {
    return ConstantC * t * t * t - ConstantA * t * t;
}

float BackOut(float t) {
    return 1.0 + ConstantC * pow(t - 1.0, 3.0) + ConstantA * pow(t - 1.0, 2.0);
}

float BackInOut(float t) {
    return t < 0.5 ?
        pow(2.0 * t, 2.0) * ((ConstantB + 1.0) * 2.0 * t - ConstantB) / 2.0 :
        (pow(2.0 * t - 2.0, 2.0) * ((ConstantB + 1.0) * (t * 2.0 - 2.0) + ConstantB) + 2.0) / 2.0;
}

float ElasticIn(float t) {
    return t == 0.0 ? 0.0 : t == 1.0 ? 1.0 :
        -pow(2.0, 10.0 * t - 10.0) * sin((t * 10.0 - 10.75) * ConstantD);
}

float ElasticOut(float t) {
    return t == 0.0 ? 0.0 : t == 1.0 ? 1.0 :
        pow(2.0, -10.0 * t) * sin((t * 10.0 - 0.75) * ConstantD) + 1.0;
}

float ElasticInOut(float t) {
    return t == 0.0 ? 0.0 : t == 1.0 ? 1.0 :
        t < 0.5 ?
            -(pow(2.0, 20.0 * t - 10.0) * sin((20.0 * t - 11.125) * ConstantE)) / 2.0 :
            pow(2.0, -20.0 * t + 10.0) * sin((20.0 * t - 11.125) * ConstantE) / 2.0 + 1.0;
}

float BounceOut(float t) {
    if (t < 1.0 / ConstantG)
        return ConstantF * t * t;
    else if (t < 2.0 / ConstantG)
        return ConstantF * (t -= 1.5 / ConstantG) * t + 0.75;
    else if (t < 2.5 / ConstantG)
        return ConstantF * (t -= 2.25 / ConstantG) * t + 0.9375;
    else
        return ConstantF * (t -= 2.625 / ConstantG) * t + 0.984375;
}

float BounceIn(float t) {
    return 1.0 - BounceOut(1.0 - t);
}

float BounceInOut(float t) {
    return t < 0.5 ?
        (1.0 - BounceOut(1.0 - 2.0 * t)) / 2.0 :
        (1.0 + BounceOut(2.0 * t - 1.0)) / 2.0;
}