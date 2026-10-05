using System;
using System.IO;
using SmokeSuppressor;

if (!SmokeAccumulationOutputMap.TryValidateCapturedEvidence(
        out string failure,
        out int sampleCount))
{
    Console.Error.WriteLine($"Smoke accumulation output map failed: {failure}");
    return 1;
}

Console.WriteLine(
    $"Smoke accumulation output map passed: {sampleCount} captured samples");

if (!SmokeNativeExpressionMap.TryValidateCapturedEvidence(
        out failure,
        out sampleCount))
{
    Console.Error.WriteLine(
        $"Smoke native expression map failed: {failure}");
    return 1;
}

Console.WriteLine(
    $"Smoke native expression map passed: {sampleCount} captured samples");

if (args.Length == 2 &&
    string.Equals(args[0], "--game-root", StringComparison.Ordinal))
{
    string gameRoot = args[1];
    if (!NativeBuildFingerprint.TryMatchUnityPlayer(
            Path.Combine(gameRoot, "UnityPlayer.dll"),
            out NativeFileFingerprint unityPlayer,
            out string fingerprintResult))
    {
        Console.Error.WriteLine(
            $"UnityPlayer fingerprint failed: {fingerprintResult}");
        return 1;
    }

    Console.WriteLine(
        $"UnityPlayer fingerprint passed: {fingerprintResult}");

    if (!NativeBuildFingerprint.TryMatchGameAssembly(
            Path.Combine(gameRoot, "GameAssembly.dll"),
            out NativeFileFingerprint gameAssembly,
            out fingerprintResult))
    {
        Console.Error.WriteLine(
            $"GameAssembly fingerprint failed: {fingerprintResult}");
        return 1;
    }

    Console.WriteLine(
        $"GameAssembly fingerprint passed: {fingerprintResult}");
}
else if (args.Length != 0)
{
    Console.Error.WriteLine(
        "Usage: OutputMapCheck [--game-root <Sprocket directory>]");
    return 2;
}

return 0;
