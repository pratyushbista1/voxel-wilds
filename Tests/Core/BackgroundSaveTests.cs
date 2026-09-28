using System;
using System.Threading;
using VoxelWilds.Core;

public static class BackgroundSaveTests
{
    public static void Run()
    {
        Spec.Run("Background saves preserve immutable payloads and never overlap", () =>
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            string actualPath = null, actualPayload = null;
            var writer = new BackgroundSaveWriter((path, payload) =>
            {
                entered.Set();
                if (!release.Wait(5000)) throw new Exception("Timed out waiting for test release");
                actualPath = path; actualPayload = payload;
            });
            try
            {
                Spec.True(writer.Begin("first.vws", "snapshot"));
                Spec.True(entered.Wait(5000));
                Spec.True(writer.IsPending && !writer.Begin("second.vws", "newer"));
                Spec.True(!writer.TryComplete(out _));
            }
            finally { release.Set(); writer.Flush(); }
            Spec.Equal("first.vws", actualPath); Spec.Equal("snapshot", actualPayload);
            Spec.True(!writer.IsPending);
        });
        Spec.Run("Background save errors reach the game and allow a later retry", () =>
        {
            int calls = 0;
            var writer = new BackgroundSaveWriter((path, payload) => { if (calls++ == 0) throw new InvalidOperationException("Disk unavailable"); });
            writer.Begin("world.vws", "snapshot");
            bool failed = false;
            try { writer.Flush(); } catch (InvalidOperationException) { failed = true; }
            Spec.True(failed && !writer.IsPending);
            Spec.True(writer.Begin("world.vws", "retry")); writer.Flush();
            Spec.Equal(2, calls);
        });
    }
}
