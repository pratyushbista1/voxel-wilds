using System;
using System.IO;
using VoxelWilds.Core;

public static class SaveTests
{
    public static void Run()
    {
        Spec.Run("atomic saves preserve a checked backup and recover corruption", () =>
        {
            string directory=Path.Combine(Path.GetTempPath(), "voxel-save-test-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);string path=Path.Combine(directory,"world.vws");
            try
            {
                SaveFile.Write(path,"first");Spec.Equal("first",SaveFile.Read(path,out bool recovered));Spec.True(!recovered);
                SaveFile.Write(path,"second");Spec.Equal("first",SaveFile.ReadOne(path+".bak"));
                File.WriteAllText(path,"broken");Spec.Equal("first",SaveFile.Read(path,out recovered));Spec.True(recovered);
                SaveFile.Write(path,"third");Spec.Equal("third",SaveFile.ReadOne(path));Spec.Equal("first",SaveFile.ReadOne(path+".bak"));
                Spec.Equal(1,Directory.GetFiles(directory,"*.corrupt-*").Length);
            }
            finally { Directory.Delete(directory,true); }
        });
    }
}
