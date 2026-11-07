using System.Runtime.CompilerServices;

namespace PartyKing.Domain.Tests;

/// <summary>
/// Compares a generated snapshot with the one checked in under <c>Golden/</c> and names the first differing line. With
/// <c>PARTYKING_UPDATE_GOLDEN=1</c> the snapshot is written instead, for an intended change of behaviour (review the diff).
/// </summary>
internal static class GoldenFiles
{
    public static void AssertMatches(string file, string actual, [CallerFilePath] string testFile = "")
    {
        if (Environment.GetEnvironmentVariable("PARTYKING_UPDATE_GOLDEN") == "1")
        {
            var path = Path.Combine(Path.GetDirectoryName(testFile)!, "Golden", file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);

            return;
        }

        // A checkout with core.autocrlf turns the snapshot into CRLF; the text is the same
        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", file)).Replace("\r\n", "\n");
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        for (var i = 0; i < Math.Max(expectedLines.Length, actualLines.Length); i++)
        {
            var wanted = i < expectedLines.Length ? expectedLines[i] : "<no line>";
            var got = i < actualLines.Length ? actualLines[i] : "<no line>";

            Assert.True(
                wanted == got,
                $"Scenario {i + 1} differs.\nExpected: {wanted}\nActual:   {got}");
        }
    }
}
