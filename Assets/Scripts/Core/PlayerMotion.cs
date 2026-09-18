using System;

namespace VoxelWilds.Core
{
    public static class PlayerMotion
    {
        public const float WalkSpeed = 4.317f;
        public const float SprintSpeed = 5.612f;
        public const float SneakSpeed = 1.3f;
        public const float Gravity = 24;
        public const float JumpSpeed = 7.745967f;
        public const float TerminalSpeed = 45;
        public const float GaitRadiansPerMetre = (float)Math.PI * .75f;

        public static bool CanSprint(float forward, bool requested, bool sneaking, bool creative,
            float hunger, bool swimming, bool flying, bool usingItem)
        {
            return requested && forward > 0 && !sneaking && !swimming && !flying && !usingItem
                && (creative || hunger > 6);
        }

        public static void NormalizeInput(ref float x, ref float z)
        {
            float length = (float)Math.Sqrt(x * x + z * z);
            if (length <= 1) return;
            x /= length;
            z /= length;
        }

        public static float IntegrateVelocity(float velocity, float target, float response, float dt, out float next)
        {
            if (dt <= 0) { next = velocity; return 0; }
            if (response <= 0) { next = velocity; return velocity * dt; }
            float decay = (float)Math.Exp(-response * dt);
            next = target + (velocity - target) * decay;
            return target * dt + (velocity - target) * (1 - decay) / response;
        }

        public static float IntegrateGravity(float velocity, float dt, out float next)
        {
            if (dt <= 0) { next = velocity; return 0; }
            velocity = Math.Max(-TerminalSpeed, velocity);
            float fallingTime = Math.Min(dt, (velocity + TerminalSpeed) / Gravity);
            next = Math.Max(-TerminalSpeed, velocity - Gravity * dt);
            return velocity * fallingTime - .5f * Gravity * fallingTime * fallingTime
                - TerminalSpeed * (dt - fallingTime);
        }

        public static float AdvanceGait(float phase, float travelledDistance, bool walking)
        {
            if (!walking || travelledDistance <= 0) return phase;
            return (phase + travelledDistance * GaitRadiansPerMetre) % ((float)Math.PI * 2);
        }
    }
}
