using System;
using System.Reflection;

public static class Spec
{
    private static int passed;
    private static int failed;
    public static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + error); }
    }
    public static void True(bool value, string message = "Expected true")
    {
        if (!value) throw new Exception(message);
    }
    public static void Equal<T>(T expected, T actual, string message = "Values differ")
    {
        if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception(message + "; expected=" + expected + ", actual=" + actual);
    }
    public static int Main()
    {
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (!type.Name.EndsWith("Tests", StringComparison.Ordinal)) continue;
            var run = type.GetMethod("Run", BindingFlags.Static | BindingFlags.Public);
            if (run == null || run.GetParameters().Length != 0) continue;
            try { run.Invoke(null, null); }
            catch (Exception error) { failed++; Console.Error.WriteLine(type.Name + ": " + error); }
        }
        Console.WriteLine(passed + " passed, " + failed + " failed");
        return failed == 0 && passed > 0 ? 0 : 1;
    }
}
