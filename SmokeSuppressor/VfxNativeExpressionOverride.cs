using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine.VFX;

namespace SmokeSuppressor
{
    internal static class VfxNativeExpressionOverride
    {
        // UnityEngine.Object.m_CachedPtr in the current IL2CPP metadata.
        private const int CachedNativeObjectOffset = 0x10;

        // UnityPlayer 2022.3.62f2 VisualEffect native object field recovered
        // from UnityEngine.VFX.VisualEffect::SetBool.
        private const int ExpressionValueBufferOffset = 0x248;
        private const int ValueStride = sizeof(int);

        private const uint MemCommit = 0x1000;
        private const uint PageNoAccess = 0x01;
        private const uint PageReadOnly = 0x02;
        private const uint PageReadWrite = 0x04;
        private const uint PageWriteCopy = 0x08;
        private const uint PageExecute = 0x10;
        private const uint PageExecuteRead = 0x20;
        private const uint PageExecuteReadWrite = 0x40;
        private const uint PageExecuteWriteCopy = 0x80;
        private const uint PageGuard = 0x100;

        internal static bool TryInitialize(out string result)
        {
            try
            {
                string gameRoot = BepInEx.Paths.GameRootPath;
                string unityPlayerPath = Path.Combine(
                    gameRoot,
                    "UnityPlayer.dll");
                string gameAssemblyPath = Path.Combine(
                    gameRoot,
                    "GameAssembly.dll");

                if (!NativeBuildFingerprint.TryMatchUnityPlayer(
                        unityPlayerPath,
                        out NativeFileFingerprint unityPlayerBuild,
                        out string unityPlayerResult))
                {
                    result = $"UnityPlayer mismatch: {unityPlayerResult}";
                    return false;
                }

                if (!NativeBuildFingerprint.TryMatchGameAssembly(
                        gameAssemblyPath,
                        out NativeFileFingerprint gameAssemblyBuild,
                        out string gameAssemblyResult))
                {
                    result = $"GameAssembly mismatch: {gameAssemblyResult}";
                    return false;
                }

                result =
                    $"UnityPlayer={unityPlayerBuild.Name}/" +
                    $"{unityPlayerBuild.Sha256}," +
                    $"GameAssembly={gameAssemblyBuild.Name}/" +
                    $"{gameAssemblyBuild.Sha256}," +
                    $"cachedPtrOffset=0x{CachedNativeObjectOffset:X}," +
                    $"valueBufferOffset=0x{ExpressionValueBufferOffset:X}";
                return true;
            }
            catch (Exception exception)
            {
                result = $"fingerprint failed: {exception}";
                return false;
            }
        }

        internal static bool TryDisableMappedExpression(
            VisualEffect visualEffect,
            out NativeExpressionWrite write,
            out string failure)
        {
            write = default;
            failure = string.Empty;

            try
            {
                if (visualEffect == null ||
                    visualEffect.visualEffectAsset == null)
                {
                    failure = "missing VisualEffect instance or asset";
                    return false;
                }

                string assetName =
                    visualEffect.visualEffectAsset.name ?? string.Empty;
                if (!SmokeNativeExpressionMap.TryGetProfile(
                        assetName,
                        out SmokeNativeExpressionProfile profile))
                {
                    failure = $"unsupported asset={assetName}";
                    return false;
                }

                if (!TryGetNativeVisualEffect(
                        visualEffect,
                        out IntPtr nativeVisualEffect,
                        out failure))
                    return false;

                IntPtr valueBufferPointerAddress = IntPtr.Add(
                    nativeVisualEffect,
                    ExpressionValueBufferOffset);
                if (!IsReadable(valueBufferPointerAddress, IntPtr.Size))
                {
                    failure =
                        $"unreadable value-buffer pointer address=" +
                        $"0x{valueBufferPointerAddress.ToInt64():X}";
                    return false;
                }

                IntPtr valueBuffer = Marshal.ReadIntPtr(
                    valueBufferPointerAddress);
                if (valueBuffer == IntPtr.Zero)
                {
                    failure = "expression value buffer is null";
                    return false;
                }

                IntPtr targetAddress = IntPtr.Add(
                    valueBuffer,
                    checked(profile.TargetValueIndex * ValueStride));
                if (!IsWritable(targetAddress, sizeof(int)))
                {
                    failure =
                        $"target expression address is not writable=" +
                        $"0x{targetAddress.ToInt64():X}";
                    return false;
                }

                var sentinels = new List<string>();
                foreach (int sentinelIndex in profile.SentinelValueIndices)
                {
                    IntPtr sentinelAddress = IntPtr.Add(
                        valueBuffer,
                        checked(sentinelIndex * ValueStride));
                    if (!IsReadable(sentinelAddress, sizeof(int)))
                    {
                        failure =
                            $"sentinel expression {sentinelIndex} " +
                            "is unreadable";
                        return false;
                    }

                    int sentinelValue = Marshal.ReadInt32(sentinelAddress);
                    if (sentinelValue != 0 && sentinelValue != 1)
                    {
                        failure =
                            $"sentinel expression {sentinelIndex} " +
                            $"has unexpected bool value={sentinelValue}";
                        return false;
                    }

                    sentinels.Add($"{sentinelIndex}={sentinelValue}");
                }

                int before = Marshal.ReadInt32(targetAddress);
                if (before != 0 && before != 1)
                {
                    failure =
                        $"target expression {profile.TargetValueIndex} " +
                        $"has unexpected bool value={before}";
                    return false;
                }

                Marshal.WriteInt32(targetAddress, 0);
                int after = Marshal.ReadInt32(targetAddress);
                if (after != 0)
                {
                    failure =
                        $"target expression write did not persist, " +
                        $"before={before},after={after}";
                    return false;
                }

                write = new NativeExpressionWrite(
                    assetName,
                    profile.SystemPath,
                    profile.TargetValueIndex,
                    before,
                    after,
                    string.Join(";", sentinels),
                    nativeVisualEffect,
                    valueBuffer);
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.ToString();
                return false;
            }
        }

        internal static bool TryZeroMappedFloat2Expression(
            VisualEffect visualEffect,
            out NativeFloat2ExpressionWrite write,
            out string failure)
        {
            write = default;
            failure = string.Empty;

            try
            {
                if (visualEffect == null ||
                    visualEffect.visualEffectAsset == null)
                {
                    failure = "missing VisualEffect instance or asset";
                    return false;
                }

                string assetName =
                    visualEffect.visualEffectAsset.name ?? string.Empty;
                if (!SmokeNativeExpressionMap.TryGetFloat2Profile(
                        assetName,
                        out SmokeNativeFloat2ExpressionProfile profile))
                {
                    failure = $"unsupported float2 asset={assetName}";
                    return false;
                }

                if (!TryGetNativeVisualEffect(
                        visualEffect,
                        out IntPtr nativeVisualEffect,
                        out failure))
                    return false;

                IntPtr valueBufferPointerAddress = IntPtr.Add(
                    nativeVisualEffect,
                    ExpressionValueBufferOffset);
                if (!IsReadable(valueBufferPointerAddress, IntPtr.Size))
                {
                    failure =
                        $"unreadable value-buffer pointer address=" +
                        $"0x{valueBufferPointerAddress.ToInt64():X}";
                    return false;
                }

                IntPtr valueBuffer = Marshal.ReadIntPtr(
                    valueBufferPointerAddress);
                if (valueBuffer == IntPtr.Zero)
                {
                    failure = "expression value buffer is null";
                    return false;
                }

                IntPtr targetAddress = IntPtr.Add(
                    valueBuffer,
                    checked(profile.TargetValueIndex * ValueStride));
                int targetByteLength = checked(2 * ValueStride);
                if (!IsWritable(targetAddress, targetByteLength))
                {
                    failure =
                        $"float2 expression address is not writable=" +
                        $"0x{targetAddress.ToInt64():X}";
                    return false;
                }

                var sentinelValues = new List<(int Index, int Value)>();
                foreach (int sentinelIndex in profile.SentinelValueIndices)
                {
                    IntPtr sentinelAddress = IntPtr.Add(
                        valueBuffer,
                        checked(sentinelIndex * ValueStride));
                    if (!IsReadable(sentinelAddress, sizeof(int)))
                    {
                        failure =
                            $"sentinel expression {sentinelIndex} " +
                            "is unreadable";
                        return false;
                    }

                    int sentinelValue = Marshal.ReadInt32(sentinelAddress);
                    if (sentinelValue != 0 && sentinelValue != 1)
                    {
                        failure =
                            $"sentinel expression {sentinelIndex} " +
                            $"has unexpected bool value={sentinelValue}";
                        return false;
                    }

                    sentinelValues.Add((sentinelIndex, sentinelValue));
                }

                float beforeX = BitConverter.Int32BitsToSingle(
                    Marshal.ReadInt32(targetAddress));
                float beforeY = BitConverter.Int32BitsToSingle(
                    Marshal.ReadInt32(targetAddress, ValueStride));
                bool alreadyZero = beforeX == 0.0f && beforeY == 0.0f;
                bool matchesCapturedConstant =
                    beforeX == profile.ExpectedX &&
                    beforeY == profile.ExpectedY;
                if (!alreadyZero && !matchesCapturedConstant)
                {
                    failure =
                        $"float2 expression {profile.TargetValueIndex} " +
                        $"has unexpected value=({beforeX:R},{beforeY:R})," +
                        $"expected=({profile.ExpectedX:R},{profile.ExpectedY:R})";
                    return false;
                }

                Marshal.WriteInt32(targetAddress, 0);
                Marshal.WriteInt32(targetAddress, ValueStride, 0);
                float afterX = BitConverter.Int32BitsToSingle(
                    Marshal.ReadInt32(targetAddress));
                float afterY = BitConverter.Int32BitsToSingle(
                    Marshal.ReadInt32(targetAddress, ValueStride));
                if (afterX != 0.0f || afterY != 0.0f)
                {
                    failure =
                        "float2 expression write did not persist, " +
                        $"before=({beforeX:R},{beforeY:R})," +
                        $"after=({afterX:R},{afterY:R})";
                    return false;
                }

                var sentinels = new List<string>();
                foreach ((int sentinelIndex, int sentinelBefore) in
                         sentinelValues)
                {
                    IntPtr sentinelAddress = IntPtr.Add(
                        valueBuffer,
                        checked(sentinelIndex * ValueStride));
                    int sentinelAfter = Marshal.ReadInt32(sentinelAddress);
                    if (sentinelAfter != sentinelBefore)
                    {
                        failure =
                            $"sentinel expression {sentinelIndex} changed " +
                            $"from {sentinelBefore} to {sentinelAfter}";
                        return false;
                    }

                    sentinels.Add(
                        $"{sentinelIndex}={sentinelBefore}->{sentinelAfter}");
                }

                write = new NativeFloat2ExpressionWrite(
                    assetName,
                    profile.SystemPath,
                    profile.ExpressionIndex,
                    profile.TargetValueIndex,
                    beforeX,
                    beforeY,
                    afterX,
                    afterY,
                    alreadyZero,
                    string.Join(";", sentinels),
                    nativeVisualEffect,
                    valueBuffer);
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.ToString();
                return false;
            }
        }

        internal static bool TryGetNativeVisualEffect(
            VisualEffect visualEffect,
            out IntPtr nativeVisualEffect,
            out string failure)
        {
            nativeVisualEffect = IntPtr.Zero;
            failure = string.Empty;

            if (visualEffect == null || visualEffect.Pointer == IntPtr.Zero)
            {
                failure = "missing VisualEffect instance";
                return false;
            }

            IntPtr cachedPtrAddress = IntPtr.Add(
                visualEffect.Pointer,
                CachedNativeObjectOffset);
            if (!IsReadable(cachedPtrAddress, IntPtr.Size))
            {
                failure =
                    $"unreadable m_CachedPtr address=" +
                    $"0x{cachedPtrAddress.ToInt64():X}";
                return false;
            }

            nativeVisualEffect = Marshal.ReadIntPtr(cachedPtrAddress);
            if (nativeVisualEffect == IntPtr.Zero)
            {
                failure = "m_CachedPtr is null";
                return false;
            }

            return true;
        }

        internal static bool IsReadable(IntPtr address, int size)
        {
            return TryQueryMemory(address, size, requireWrite: false);
        }

        internal static bool IsWritable(IntPtr address, int size)
        {
            return TryQueryMemory(address, size, requireWrite: true);
        }

        private static bool TryQueryMemory(
            IntPtr address,
            int size,
            bool requireWrite)
        {
            if (address == IntPtr.Zero || size <= 0)
                return false;

            UIntPtr queryResult = VirtualQuery(
                address,
                out MemoryBasicInformation information,
                (UIntPtr)Marshal.SizeOf<MemoryBasicInformation>());
            if (queryResult == UIntPtr.Zero ||
                information.State != MemCommit ||
                (information.Protect & (PageNoAccess | PageGuard)) != 0)
            {
                return false;
            }

            uint baseProtection = information.Protect & 0xff;
            bool protectionMatches = requireWrite
                ? baseProtection == PageReadWrite ||
                  baseProtection == PageWriteCopy ||
                  baseProtection == PageExecuteReadWrite ||
                  baseProtection == PageExecuteWriteCopy
                : baseProtection == PageReadOnly ||
                  baseProtection == PageReadWrite ||
                  baseProtection == PageWriteCopy ||
                  baseProtection == PageExecuteRead ||
                  baseProtection == PageExecuteReadWrite ||
                  baseProtection == PageExecuteWriteCopy;
            if (!protectionMatches)
                return false;

            ulong start = unchecked((ulong)address.ToInt64());
            ulong end = checked(start + (ulong)size);
            ulong regionStart = unchecked(
                (ulong)information.BaseAddress.ToInt64());
            ulong regionEnd = checked(
                regionStart + information.RegionSize.ToUInt64());
            return start >= regionStart && end <= regionEnd;
        }

        [DllImport("kernel32.dll")]
        private static extern UIntPtr VirtualQuery(
            IntPtr address,
            out MemoryBasicInformation buffer,
            UIntPtr length);

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryBasicInformation
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public ushort PartitionId;
            public UIntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

    }

    internal readonly struct NativeExpressionWrite
    {
        public NativeExpressionWrite(
            string assetName,
            string systemPath,
            int valueIndex,
            int before,
            int after,
            string sentinels,
            IntPtr nativeVisualEffect,
            IntPtr valueBuffer)
        {
            AssetName = assetName;
            SystemPath = systemPath;
            ValueIndex = valueIndex;
            Before = before;
            After = after;
            Sentinels = sentinels;
            NativeVisualEffect = nativeVisualEffect;
            ValueBuffer = valueBuffer;
        }

        public string AssetName { get; }
        public string SystemPath { get; }
        public int ValueIndex { get; }
        public int Before { get; }
        public int After { get; }
        public string Sentinels { get; }
        public IntPtr NativeVisualEffect { get; }
        public IntPtr ValueBuffer { get; }
    }

    internal readonly struct NativeFloat2ExpressionWrite
    {
        public NativeFloat2ExpressionWrite(
            string assetName,
            string systemPath,
            int expressionIndex,
            int valueIndex,
            float beforeX,
            float beforeY,
            float afterX,
            float afterY,
            bool alreadyZero,
            string sentinels,
            IntPtr nativeVisualEffect,
            IntPtr valueBuffer)
        {
            AssetName = assetName;
            SystemPath = systemPath;
            ExpressionIndex = expressionIndex;
            ValueIndex = valueIndex;
            BeforeX = beforeX;
            BeforeY = beforeY;
            AfterX = afterX;
            AfterY = afterY;
            AlreadyZero = alreadyZero;
            Sentinels = sentinels;
            NativeVisualEffect = nativeVisualEffect;
            ValueBuffer = valueBuffer;
        }

        public string AssetName { get; }
        public string SystemPath { get; }
        public int ExpressionIndex { get; }
        public int ValueIndex { get; }
        public float BeforeX { get; }
        public float BeforeY { get; }
        public float AfterX { get; }
        public float AfterY { get; }
        public bool AlreadyZero { get; }
        public string Sentinels { get; }
        public IntPtr NativeVisualEffect { get; }
        public IntPtr ValueBuffer { get; }
    }
}
