using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Any warning, error or exception a test did not declare with LogAssert.Expect fails that test.
[assembly: TestMustExpectAllLogs]

/// <summary>Drops plain Debug.Log output for the run so only warnings and above reach the unexpected-log check.</summary>
[SetUpFixture]
public class WarningsAndAboveOnlyFixture
{
    private LogType previous;

    [OneTimeSetUp]
    public void FilterInfoLogs()
    {
        previous = Debug.unityLogger.filterLogType;
        Debug.unityLogger.filterLogType = LogType.Warning;
    }

    [OneTimeTearDown]
    public void RestoreLogFilter()
    {
        Debug.unityLogger.filterLogType = previous;
    }
}
