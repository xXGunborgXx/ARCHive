using ARCHive.Core;

namespace ARCHive.UnitTests;

[TestClass]
public sealed class TransferRateEstimatorTests
{
    [TestMethod]
    public void Update_AfterSecondSample_ReportsMeasuredSpeed()
    {
        var estimator = new TransferRateEstimator();

        estimator.Update(0, TimeSpan.Zero);
        var result = estimator.Update(
            200,
            TimeSpan.FromSeconds(1));

        Assert.AreEqual(200d, result.BytesPerSecond);
    }

    [TestMethod]
    public void Update_SmoothsShortTermSpeedChanges()
    {
        var estimator = new TransferRateEstimator();

        estimator.Update(0, TimeSpan.Zero);
        estimator.Update(100, TimeSpan.FromSeconds(1));
        var result = estimator.Update(
            300,
            TimeSpan.FromSeconds(2));

        Assert.AreEqual(130d, result.BytesPerSecond);
    }

    [TestMethod]
    public void Reset_RequiresANewPairOfSamples()
    {
        var estimator = new TransferRateEstimator();
        estimator.Update(0, TimeSpan.Zero);
        estimator.Update(100, TimeSpan.FromSeconds(1));

        estimator.Reset();
        var result = estimator.Update(
            500,
            TimeSpan.FromSeconds(2));

        Assert.IsNull(result.BytesPerSecond);
    }

    [TestMethod]
    public void Update_AfterTwoSecondsWithoutMovement_ReportsWaiting()
    {
        var estimator = new TransferRateEstimator();

        estimator.Update(100, TimeSpan.Zero);
        var result = estimator.Update(
            100,
            TimeSpan.FromSeconds(2));

        Assert.IsTrue(result.IsWaitingForProgress);
        Assert.IsNull(result.BytesPerSecond);
    }
}
