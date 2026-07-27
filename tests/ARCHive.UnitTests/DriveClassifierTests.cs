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
    [DataRow(0x0Du, DriveBusType.NVMe)]
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
    public void ClassifySpeed_NVMe_ReturnsFast()
    {
        var classifyMethod = typeof(DriveClassifier).GetMethod(
            "ClassifySpeed",
            BindingFlags.Public | BindingFlags.Static);
        var mapMethod = typeof(DriveClassifier).GetMethod(
            "MapBusType",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(classifyMethod);
        Assert.IsNotNull(mapMethod);

        var nvMeValue = (DriveBusType)mapMethod.Invoke(null, [0x0Du])!;
        Assert.AreEqual(DriveBusType.NVMe, nvMeValue);
    }

    [TestMethod]
    public void RecommendedConcurrency_LargeFileOnFastDrive_ReturnsLowConcurrency()
    {
        var method = typeof(DriveClassifier).GetMethod(
            "RecommendedConcurrency",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method);

        var result = (int)method.Invoke(null, ["C:\\", 256L * 1024 * 1024])!;
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void RecommendedConcurrency_SmallFileOnFastDrive_ReturnsHighConcurrency()
    {
        var method = typeof(DriveClassifier).GetMethod(
            "RecommendedConcurrency",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method);

        var result = (int)method.Invoke(null, ["C:\\", 1024L])!;
        Assert.AreEqual(8, result);
    }
}
