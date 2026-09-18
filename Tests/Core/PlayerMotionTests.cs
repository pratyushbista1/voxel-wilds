using System;
using VoxelWilds.Core;

public static class PlayerMotionTests
{
    public static void Run()
    {
        Spec.Run("Walking acceleration and travel are consistent at 30, 60 and 144 FPS", () =>
        {
            float reference = Travel(60, PlayerMotion.WalkSpeed, out float speed);
            Close(PlayerMotion.WalkSpeed, speed, .001f);
            foreach (int fps in new[] { 30, 144 }) Close(reference, Travel(fps, PlayerMotion.WalkSpeed, out _), .001f);
        });
        Spec.Run("Sprinting reaches its target without exceeding it", () =>
        {
            foreach (int fps in new[] { 30, 60, 144 })
            {
                float distance = Travel(fps, PlayerMotion.SprintSpeed, out float speed);
                Close(PlayerMotion.SprintSpeed, speed, .001f);
                Spec.True(distance < PlayerMotion.SprintSpeed * 2 && distance > PlayerMotion.WalkSpeed * 2);
            }
        });
        Spec.Run("Ground braking has consistent stopping distance at different frame rates", () =>
        {
            float reference = Stop(60);
            foreach (int fps in new[] { 30, 144 }) Close(reference, Stop(fps), .001f);
            Spec.True(reference < .26f, "Releasing movement must not feel like sliding on ice");
        });
        Spec.Run("Diagonal input cannot move faster than straight input", () =>
        {
            float x = 1, z = 1;
            PlayerMotion.NormalizeInput(ref x, ref z);
            Close(1, x * x + z * z, .00001f);
            x = .2f; z = .3f;
            PlayerMotion.NormalizeInput(ref x, ref z);
            Close(.2f, x, .00001f); Close(.3f, z, .00001f);
        });
        Spec.Run("Sprint requires forward movement and enough food or creative mode", () =>
        {
            Spec.True(PlayerMotion.CanSprint(1, true, false, false, 7, false, false, false));
            Spec.True(PlayerMotion.CanSprint(1, true, false, true, 0, false, false, false));
            Spec.True(!PlayerMotion.CanSprint(1, true, false, false, 6, false, false, false));
            Spec.True(!PlayerMotion.CanSprint(0, true, false, false, 20, false, false, false));
            Spec.True(!PlayerMotion.CanSprint(-1, true, false, false, 20, false, false, false));
            Spec.True(!PlayerMotion.CanSprint(1, false, false, false, 20, false, false, false));
        });
        Spec.Run("Sneaking, swimming, flying and item use do not activate ground sprint", () =>
        {
            Spec.True(!PlayerMotion.CanSprint(1, true, true, false, 20, false, false, false));
            Spec.True(!PlayerMotion.CanSprint(1, true, false, false, 20, true, false, false));
            Spec.True(!PlayerMotion.CanSprint(1, true, false, true, 20, false, true, false));
            Spec.True(!PlayerMotion.CanSprint(1, true, false, false, 20, false, false, true));
        });
        Spec.Run("Jump clears one block with stable height at 30, 60 and 144 FPS", () =>
        {
            foreach (int fps in new[] { 30, 60, 144 })
            {
                float velocity = PlayerMotion.JumpSpeed, height = 0, apex = 0;
                for (int frame = 0; frame < fps; frame++)
                {
                    height += PlayerMotion.IntegrateGravity(velocity, 1f / fps, out velocity);
                    apex = Math.Max(apex, height);
                }
                Close(1.25f, apex, .004f);
                Spec.True(height < 0, "A jump returns to ground in less than one second");
            }
        });
        Spec.Run("Falling reaches terminal speed without frame dependent travel", () =>
        {
            float reference = Fall(60);
            foreach (int fps in new[] { 30, 144 }) Close(reference, Fall(fps), .002f);
        });
        Spec.Run("Air steering responds more slowly than grounded steering", () =>
        {
            PlayerMotion.IntegrateVelocity(PlayerMotion.WalkSpeed, -PlayerMotion.WalkSpeed, 3, .1f, out float air);
            PlayerMotion.IntegrateVelocity(PlayerMotion.WalkSpeed, -PlayerMotion.WalkSpeed, 18, .1f, out float ground);
            Spec.True(air > 0 && ground < 0);
        });
        Spec.Run("Idle, blocked and airborne motion do not advance the walk cycle", () =>
        {
            Close(1, PlayerMotion.AdvanceGait(1, 0, true), .00001f);
            Close(1, PlayerMotion.AdvanceGait(1, 1, false), .00001f);
            Close(1, PlayerMotion.AdvanceGait(1, -1, true), .00001f);
        });
        Spec.Run("Walk cycle is distance driven and does not depend on frame rate", () =>
        {
            foreach (int fps in new[] { 30, 60, 144 })
            {
                float phase = 0;
                for (int i = 0; i < fps; i++) phase = PlayerMotion.AdvanceGait(phase, 1f / fps, true);
                Close(PlayerMotion.GaitRadiansPerMetre, phase, .0001f);
            }
        });
    }

    private static float Travel(int fps, float target, out float velocity)
    {
        velocity = 0;
        float distance = 0;
        for (int frame = 0; frame < fps * 2; frame++)
            distance += PlayerMotion.IntegrateVelocity(velocity, target, 18, 1f / fps, out velocity);
        return distance;
    }

    private static float Stop(int fps)
    {
        float velocity = PlayerMotion.SprintSpeed, distance = 0;
        for (int frame = 0; frame < fps; frame++)
            distance += PlayerMotion.IntegrateVelocity(velocity, 0, 22, 1f / fps, out velocity);
        Close(0, velocity, .001f);
        return distance;
    }

    private static float Fall(int fps)
    {
        float velocity = 0, height = 0;
        for (int frame = 0; frame < fps * 4; frame++)
            height += PlayerMotion.IntegrateGravity(velocity, 1f / fps, out velocity);
        Close(-PlayerMotion.TerminalSpeed, velocity, .0001f);
        return height;
    }

    private static void Close(float expected, float actual, float tolerance)
        => Spec.True(Math.Abs(expected - actual) <= tolerance, "Expected " + expected + ", actual " + actual);
}
