using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text;

namespace ARCHive.Copy;

public enum DriveBusType
{
    Unknown,
    SCSI,
    SATA,
    NVMe,
    USB,
    Network,
    Virtual
}

public enum DriveSpeedClass
{
    Unknown,
    Slow,
    Medium,
    Fast
}

public static class DriveClassifier
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
    private const uint PropertyStandardQuery = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public uint PropertyId;
        public uint QueryType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_DESCRIPTOR_HEADER
    {
        public uint Version;
        public uint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_DEVICE_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        public byte DeviceType;
        public byte DeviceTypeModifier;
        public byte RemovableMedia;
        public byte CommandQueueing;
        public uint VendorIdOffset;
        public uint ProductIdOffset;
        public uint ProductRevisionOffset;
        public uint SerialNumberOffset;
        public uint StorageBusType;
        public uint RawPropertiesLength;
    }

    public static DriveBusType GetBusType(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root))
            {
                return DriveBusType.Unknown;
            }

            var devicePath = @"\\.\" + root.TrimEnd('\\');

            using var handle = CreateFileW(
                devicePath,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                return FallbackBusType(path);
            }

            var headerSize = (uint)Marshal.SizeOf<STORAGE_DESCRIPTOR_HEADER>();
            var headerPtr = Marshal.AllocHGlobal((int)headerSize);
            try
            {
                var query = new STORAGE_PROPERTY_QUERY
                {
                    PropertyId = 0,
                    QueryType = PropertyStandardQuery
                };

                var querySize = (uint)Marshal.SizeOf<STORAGE_PROPERTY_QUERY>();
                var queryPtr = Marshal.AllocHGlobal((int)querySize);
                try
                {
                    Marshal.StructureToPtr(query, queryPtr, false);

                    var success = DeviceIoControl(
                        handle,
                        IOCTL_STORAGE_QUERY_PROPERTY,
                        queryPtr,
                        querySize,
                        headerPtr,
                        headerSize,
                        out _,
                        IntPtr.Zero);

                    if (!success)
                    {
                        return FallbackBusType(path);
                    }

                    var header = Marshal.PtrToStructure<STORAGE_DESCRIPTOR_HEADER>(headerPtr);
                    var descriptorSize = header.Size;
                    var descriptorPtr = Marshal.AllocHGlobal((int)descriptorSize);
                    try
                    {
                        success = DeviceIoControl(
                            handle,
                            IOCTL_STORAGE_QUERY_PROPERTY,
                            queryPtr,
                            querySize,
                            descriptorPtr,
                            descriptorSize,
                            out _,
                            IntPtr.Zero);

                        if (!success)
                        {
                            return FallbackBusType(path);
                        }

                        var descriptor = Marshal.PtrToStructure<STORAGE_DEVICE_DESCRIPTOR>(descriptorPtr);
                        return MapBusType(descriptor.StorageBusType);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(descriptorPtr);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(queryPtr);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(headerPtr);
            }
        }
        catch
        {
            return DriveBusType.Unknown;
        }
    }

    private static DriveBusType MapBusType(uint busType) => busType switch
    {
        0x01 => DriveBusType.SCSI,
        0x07 => DriveBusType.USB,
        0x0B => DriveBusType.SATA,
        0x0D => DriveBusType.NVMe,
        _ => DriveBusType.Unknown
    };

    private static DriveBusType FallbackBusType(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root))
            {
                return DriveBusType.Unknown;
            }

            var info = new DriveInfo(root);
            return info.DriveType switch
            {
                DriveType.Network => DriveBusType.Network,
                DriveType.Removable => DriveBusType.USB,
                DriveType.Fixed => DriveBusType.SATA,
                _ => DriveBusType.Unknown
            };
        }
        catch
        {
            return DriveBusType.Unknown;
        }
    }

    public static DriveSpeedClass ClassifySpeed(string path)
    {
        var busType = GetBusType(path);
        return busType switch
        {
            DriveBusType.NVMe => DriveSpeedClass.Fast,
            DriveBusType.SATA => DriveSpeedClass.Fast,
            DriveBusType.SCSI => DriveSpeedClass.Fast,
            DriveBusType.USB => DriveSpeedClass.Slow,
            DriveBusType.Network => DriveSpeedClass.Slow,
            DriveBusType.Virtual => DriveSpeedClass.Medium,
            _ => DriveSpeedClass.Medium
        };
    }

    public static int RecommendedConcurrency(string path, long largestFileBytes)
    {
        var speed = ClassifySpeed(path);
        return speed switch
        {
            DriveSpeedClass.Slow => 2,
            DriveSpeedClass.Fast => largestFileBytes >= 256L * 1024 * 1024 ? 2 : 8,
            _ => 4
        };
    }
}
