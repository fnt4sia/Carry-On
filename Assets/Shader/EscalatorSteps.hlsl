// Vertex path for the escalator steps, called from Shader/EscalatorSteps.shadergraph through a
// Custom Function node. The graph still works out where each step is along the loop (ZNow); this
// turns that into the step's height and shape. Everything is in the steps mesh's object space.
#ifndef ESCALATOR_STEPS_INCLUDED
#define ESCALATOR_STEPS_INCLUDED

// Measured from Escalator_Steps / Escalator_Frame (October 2026). Change them only together with
// regenerated meshes, like the graph's own properties.
static const float EscalatorStepRun = 0.9302;  // one step's depth along z
static const float EscalatorStepRise = 0.4187; // one step's height
static const float EscalatorCombZ = 7.25;      // front edge of the bottom landing plate
static const float EscalatorCombY = -3.42;     // just under that plate's surface (-3.38)
static const float EscalatorBendZ = 5.5;       // where the steps start easing down onto the bottom landing

// Height of a step's tread (its front top edge) at z. The frame's incline runs straight into the
// bottom landing plate, so on the plain clamp the last steps stood up to 0.31 proud of the plate as
// they went under it. They now ease down onto the plate's height by its front edge and arrive flat,
// the way the top end already did.
float EscalatorPathY(float z, float slope, float lineY, float bottomY, float topY)
{
    float incline = lineY - slope * z;
    float clamped = clamp(incline, bottomY, topY);
    float bent = lerp(incline, EscalatorCombY, smoothstep(EscalatorBendZ, EscalatorCombZ, z));
    return min(clamped, bent);
}

// The step one place further down decides this step's riser: where the path flattens, the riser
// folds away and the treads lie flat like a real escalator's landings; where it steepens, the riser
// stretches so no gap opens between treads. On the plain incline the ratio is exactly 1.
void EscalatorStepPosition_float(float3 Position, float4 Step, float ZNow, float Slope, float LineY,
                                 float BottomY, float TopY, out float3 Out)
{
    float restZ = Step.x;
    float restY = Step.z;
    float yNow = EscalatorPathY(ZNow, Slope, LineY, BottomY, TopY);
    float yBelow = EscalatorPathY(ZNow + EscalatorStepRun, Slope, LineY, BottomY, TopY);
    float riser = max(0.0, (yNow - yBelow) / EscalatorStepRise);
    Out = float3(Position.x, yNow + (Position.y - restY) * riser, ZNow + (Position.z - restZ));
}

void EscalatorStepPosition_half(half3 Position, half4 Step, half ZNow, half Slope, half LineY,
                                half BottomY, half TopY, out half3 Out)
{
    float3 result;
    EscalatorStepPosition_float(Position, Step, ZNow, Slope, LineY, BottomY, TopY, result);
    Out = result;
}

#endif
