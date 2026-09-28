using System;
using System.Threading.Tasks;

namespace VoxelWilds.Core
{
    public sealed class BackgroundSaveWriter
    {
        private readonly Action<string, string> write;
        private Task pending;
        public bool IsPending => pending != null;

        public BackgroundSaveWriter(Action<string, string> writer = null) { write = writer ?? SaveFile.Write; }

        public bool Begin(string path, string payload)
        {
            if (pending != null) return false;
            pending = Task.Run(() => write(path, payload));
            return true;
        }

        public bool TryComplete(out Exception error)
        {
            error = null;
            if (pending == null || !pending.IsCompleted) return false;
            try { Flush(); }
            catch (Exception caught) { error = caught; }
            return true;
        }

        public void Flush()
        {
            var task = pending;
            if (task == null) return;
            try { task.GetAwaiter().GetResult(); }
            finally { pending = null; }
        }
    }
}
