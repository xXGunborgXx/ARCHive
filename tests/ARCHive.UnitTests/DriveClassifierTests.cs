using System.Reflection;
using ARCHive.Copy;

namespace ARCHive.UnitTests;

[TestClass]
public sealed class DriveClassifierTests
{
    [TestMethod]
    [DataRow(0x01u, DriveBusType.SCSI)]
    [DataRow(0x07u, DriveBusType.USB)]
    [DataRow(0x0Bu, DriveBusType.SATA)]
    [DataRow(0x0Eu, DriveBusType.Virtual)]
    [DataRow(0x0Fu, DriveBusType.Virtual)]
    [DataRow(0x10u, DriveBusType.Virtual)]
    [DataRow(0x11u, DriveBusType.NVMe)]
    [DataRow(0x00u, DriveBusType.Unknown)]
    [DataRow(0xFFu, DriveBusType.Unknown)]
    public void MapBusType_ReturnsCorrectBusType(uint input, DriveBusType expected)
    {
        var method = typeof(DriveClassifier).GetMethod(
            "MapBusType",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);
        var result = method.Invoke(null, [input]);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(DriveBusType.NVMe, null, DriveSpeedClass.Fast)]
    [DataRow(DriveBusType.SATA, false, DriveSpeedClass.Fast)]
    [DataRow(DriveBusType.SATA, true, DriveSpeedClass.Slow)]
    [DataRow(DriveBusType.SCSI, null, DriveSpeedClass.Medium)]
    [DataRow(DriveBusType.USB, false, DriveSpeedClass.Slow)]
    public void ClassifySpeed_UsesSeekPenaltyAndBusType(
        DriveBusType busType,
        bool? incursSeekPenalty,
        DriveSpeedClass expected)
    {
        var classifyMethod = typeof(DriveClassifier).GetMethod(
            "ClassifySpeed",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(DriveBusType), typeof(bool?)],
            modifiers: null);

        Assert.IsNotNull(classifyMethod);
        var result = classifyMethod.Invoke(
            null,
            [busType, incursSeekPenalty]);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void RecommendedConcurrency_LargeFileOnFastDrive_ReturnsLowConcurrency()
    {
        var method = typeof(DriveClassifier).GetMethod(
            "RecommendedConcurrency",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(DriveSpeedClass), typeof(long)],
            modifiers: null);
        Assert.IsNotNull(method);

        var result = (int)method.Invoke(
            null,
            [DriveSpeedClass.Fast, 256L * 1024 * 1024])!;
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void RecommendedConcurrency_SmallFileOnFastDrive_ReturnsHighConcurrency()
    {
        var method = typeof(DriveClassifier).GetMethod(
            "RecommendedConcurrency",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(DriveSpeedClass), typeof(long)],
            modifiers: null);
        Assert.IsNotNull(method);

        var result = (int)method.Invoke(
            null,
            [DriveSpeedClass.Fast, 1024L])!;
        Assert.AreEqual(8, result);
    }
}
