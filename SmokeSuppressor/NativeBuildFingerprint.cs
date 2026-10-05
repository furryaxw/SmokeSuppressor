using System;
using System.IO;
using System.Security.Cryptography;

namespace SmokeSuppressor
{
    internal readonly record struct NativeFileFingerprint(
        string Name,
        long Length,
        string Sha256);

    internal static class NativeBuildFingerprint
    {
        private static readonly NativeFileFingerprint[] UnityPlayerBuilds =
        {
            new(
                "Unity 6000.3.21f1",
                36_673_448,
                "5158C9F49DB8CE4ED77A5BF10D0AD0FAEE7A9F246EC9A9C97E7CB9AE1CCCEC1D")
        };

        private static readonly NativeFileFingerprint[] GameAssemblyBuilds =
        {
            new(
                "Sprocket 0.2.55.5",
                76_794_368,
                "18A9A15B5E5F11898ED4DC34FC3E2D4C12950C3B37AC1FA499E8B00592DEDD56")
        };

        internal static bool TryMatchUnityPlayer(
            string path,
            out NativeFileFingerprint matched,
            out string result) =>
            TryMatchKnownFile(path, UnityPlayerBuilds, out matched, out result);

        internal static bool TryMatchGameAssembly(
            string path,
            out NativeFileFingerprint matched,
            out string result) =>
            TryMatchKnownFile(path, GameAssemblyBuilds, out matched, out result);

        private static bool TryMatchKnownFile(
            string path,
            NativeFileFingerprint[] knownBuilds,
            out NativeFileFingerprint matched,
            out string result)
        {
            matched = default;
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                result = $"missing path={path}";
                return false;
            }

            using FileStream stream = File.OpenRead(path);
            using SHA256 sha256 = SHA256.Create();
            string actualSha256 = Convert.ToHexString(
                sha256.ComputeHash(stream));

            foreach (NativeFileFingerprint candidate in knownBuilds)
            {
                if (file.Length == candidate.Length &&
                    string.Equals(
                        actualSha256,
                        candidate.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matched = candidate;
                    result =
                        $"build={candidate.Name},length={file.Length}," +
                        $"sha256={actualSha256}";
                    return true;
                }
            }

            string expected = string.Join(
                " or ",
                Array.ConvertAll(
                    knownBuilds,
                    candidate =>
                        $"{candidate.Name}/length={candidate.Length}/" +
                        $"sha256={candidate.Sha256}"));
            result =
                $"expected={expected},actual=length={file.Length}/" +
                $"sha256={actualSha256}";
            return false;
        }
    }
}
