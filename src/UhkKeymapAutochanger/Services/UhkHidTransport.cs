using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using UhkKeymapAutochanger.Core.Services;
using UhkKeymapAutochanger.Core.Settings;
using UhkKeymapAutochanger.Diagnostics;

namespace UhkKeymapAutochanger.Services;

internal sealed class UhkHidTransport : IKeymapTransport, IDisposable
{
    private const ushort UhkVendorId = 0x37A8;
    private const ushort Uhk80RightProductId = 0x0009;
    private const ushort LegacyUsagePage = 0x0080;
    private const ushort LegacyUsage = 0x0081;
    private const ushort CurrentUsagePage = 0xFF00;
    private const ushort CurrentUsage = 0x0001;
    private const byte SwitchKeymapCommand = 0x11;
    private const int DefaultReportLength = 65;

    private static readonly HashSet<ushort> SupportedProductIds = new()
    {
        Uhk80RightProductId,
    };

    private static readonly IntPtr InvalidHandleValue = new(-1);

    private readonly object _sync = new();
    private readonly TimeSpan _reconnectInterval;
    private readonly IDebugLogger _logger;
    private readonly byte _reportId;

    private DateTime _nextReconnectAttemptUtc = DateTime.MinValue;
    private bool _disposed;

    public UhkHidTransport(byte reportId, TimeSpan reconnectInterval, IDebugLogger logger)
    {
        _reportId = reportId;
        _reconnectInterval = reconnectInterval;
        _logger = logger;
    }

    public Task SwitchKeymapAsync(string keymapAbbreviation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedKeymap = SettingsValidator.NormalizeKeymap(keymapAbbreviation);
        if (string.IsNullOrWhiteSpace(normalizedKeymap))
        {
            throw new ArgumentException("keymapAbbreviation is required.", nameof(keymapAbbreviation));
        }

        var keymapBytes = Encoding.ASCII.GetBytes(normalizedKeymap);
        if (keymapBytes.Length != normalizedKeymap.Length)
        {
            throw new ArgumentException("keymapAbbreviation must be ASCII only.", nameof(keymapAbbreviation));
        }

        if (keymapBytes.Length > byte.MaxValue)
        {
            throw new ArgumentException("keymapAbbreviation is too long.", nameof(keymapAbbreviation));
        }

        lock (_sync)
        {
            ThrowIfDisposed();
            try
            {
                if (DateTime.UtcNow < _nextReconnectAttemptUtc)
                {
                    throw new IOException("UHK device is temporarily unavailable.");
                }

                var candidate = EnumerateCompatibleDevices().FirstOrDefault();
                if (candidate is null)
                {
                    _nextReconnectAttemptUtc = DateTime.UtcNow.Add(_reconnectInterval);
                    throw new IOException("Cannot find compatible UHK80 HID communication interface.");
                }

                using var stream = OpenDeviceStream(candidate.DevicePath, candidate.OutputReportLength);
                WriteSwitchKeymapReportLocked(stream, candidate.OutputReportLength, keymapBytes);
                _nextReconnectAttemptUtc = DateTime.MinValue;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.Log($"HID write failed: {ex.Message}");
                _nextReconnectAttemptUtc = DateTime.UtcNow.Add(_reconnectInterval);
                throw new IOException("Failed to send keymap switch HID command.", ex);
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
    }

    private static FileStream OpenDeviceStream(string devicePath, int outputReportLength)
    {
        var handle = CreateFile(
            devicePath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException($"Failed to open HID device. Win32Error={error}");
        }

        try
        {
            var reportLength = outputReportLength > 0 ? outputReportLength : DefaultReportLength;
            return new FileStream(handle, FileAccess.ReadWrite, reportLength, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw new IOException($"Failed to open HID device. Win32Error={Marshal.GetLastWin32Error()}");
        }
    }

    private void WriteSwitchKeymapReportLocked(FileStream stream, int outputReportLength, byte[] keymapBytes)
    {
        var safeOutputLength = outputReportLength > 0 ? outputReportLength : DefaultReportLength;
        var reportLength = Math.Max(safeOutputLength, keymapBytes.Length + 3);
        var report = new byte[reportLength];
        report[0] = _reportId;
        report[1] = SwitchKeymapCommand;
        report[2] = (byte)keymapBytes.Length;
        Array.Copy(keymapBytes, 0, report, 3, keymapBytes.Length);

        stream.Write(report, 0, report.Length);
        stream.Flush();
    }

    private static List<HidDeviceCandidate> EnumerateCompatibleDevices()
    {
        var candidates = new List<HidDeviceCandidate>();

        HidD_GetHidGuid(out var hidGuid);
        var infoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);
        if (infoSet == InvalidHandleValue)
        {
            return candidates;
        }

        try
        {
            var index = 0;
            while (true)
            {
                var interfaceData = new SpDeviceInterfaceData
                {
                    cbSize = Marshal.SizeOf<SpDeviceInterfaceData>(),
                };

                var enumSucceeded = SetupDiEnumDeviceInterfaces(
                    infoSet,
                    IntPtr.Zero,
                    ref hidGuid,
                    index,
                    ref interfaceData);

                if (!enumSucceeded)
                {
                    if (Marshal.GetLastWin32Error() == ErrorNoMoreItems)
                    {
                        break;
                    }

                    index++;
                    continue;
                }

                var detailData = new SpDeviceInterfaceDetailData
                {
                    cbSize = IntPtr.Size == 8 ? 8 : 6,
                    DevicePath = string.Empty,
                };

                var detailSucceeded = SetupDiGetDeviceInterfaceDetail(
                    infoSet,
                    ref interfaceData,
                    ref detailData,
                    Marshal.SizeOf<SpDeviceInterfaceDetailData>(),
                    out _,
                    IntPtr.Zero);

                if (!detailSucceeded || string.IsNullOrWhiteSpace(detailData.DevicePath))
                {
                    index++;
                    continue;
                }

                using var probeHandle = CreateFile(
                    detailData.DevicePath,
                    0,
                    FileShareRead | FileShareWrite,
                    IntPtr.Zero,
                    OpenExisting,
                    0,
                    IntPtr.Zero);

                if (probeHandle.IsInvalid)
                {
                    index++;
                    continue;
                }

                var attributes = new HiddAttributes
                {
                    Size = Marshal.SizeOf<HiddAttributes>(),
                };

                if (!HidD_GetAttributes(probeHandle, ref attributes))
                {
                    index++;
                    continue;
                }

                if (attributes.VendorID != UhkVendorId || !SupportedProductIds.Contains(attributes.ProductID))
                {
                    index++;
                    continue;
                }

                if (!TryGetCaps(probeHandle, out var caps))
                {
                    index++;
                    continue;
                }

                var usagePage = unchecked((ushort)caps.UsagePage);
                var usage = unchecked((ushort)caps.Usage);
                var isCommunicationUsage =
                    (usagePage == LegacyUsagePage && usage == LegacyUsage) ||
                    (usagePage == CurrentUsagePage && usage == CurrentUsage);

                if (!isCommunicationUsage)
                {
                    index++;
                    continue;
                }

                var outputReportLength = caps.OutputReportByteLength > 0
                    ? caps.OutputReportByteLength
                    : DefaultReportLength;

                candidates.Add(new HidDeviceCandidate(detailData.DevicePath, outputReportLength));
                index++;
            }
        }
        finally
        {
            _ = SetupDiDestroyDeviceInfoList(infoSet);
        }

        return candidates;
    }

    private static bool TryGetCaps(SafeFileHandle handle, out HidpCaps caps)
    {
        caps = default;

        if (!HidD_GetPreparsedData(handle, out var preparsedData) || preparsedData == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return HidP_GetCaps(preparsedData, out caps) == HidpStatusSuccess;
        }
        finally
        {
            _ = HidD_FreePreparsedData(preparsedData);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UhkHidTransport));
        }
    }

    private sealed record HidDeviceCandidate(string DevicePath, int OutputReportLength);

    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const int ErrorNoMoreItems = 259;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const int HidpStatusSuccess = 0x00110000;

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SpDeviceInterfaceDetailData
    {
        public int cbSize;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string DevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HiddAttributes
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public short Usage;
        public short UsagePage;
        public short InputReportByteLength;
        public short OutputReportByteLength;
        public short FeatureReportByteLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public short[] Reserved;

        public short NumberLinkCollectionNodes;
        public short NumberInputButtonCaps;
        public short NumberInputValueCaps;
        public short NumberInputDataIndices;
        public short NumberOutputButtonCaps;
        public short NumberOutputValueCaps;
        public short NumberOutputDataIndices;
        public short NumberFeatureButtonCaps;
        public short NumberFeatureValueCaps;
        public short NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HiddAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        IntPtr enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        int memberIndex,
        ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData,
        ref SpDeviceInterfaceDetailData deviceInterfaceDetailData,
        int deviceInterfaceDetailDataSize,
        out int requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}
