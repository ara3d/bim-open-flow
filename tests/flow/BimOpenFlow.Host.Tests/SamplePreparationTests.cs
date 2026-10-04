namespace BimOpenFlow.Host.Tests;

/// <summary>Background preparation of generated samples: staleness by stamp (input hash and
/// producer version), the "not ready yet" reason, building through a .part file, and the ready
/// callback. The build is injected so these run in milliseconds; the real IFC build is covered
/// by IfcDuckDbBuildTests and the NRC workflow fixture.</summary>
[TestFixture]
public sealed class SamplePreparationTests
{
    private string _dir = null!;
    private string _input = null!;
    private string _output = null!;
    private int _builds;

    [SetUp]
    public void NewDir()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bof-preparation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _input = Path.Combine(_dir, "model.ifc");
        _output = Path.Combine(_dir, "model.duckdb");
        File.WriteAllText(_input, "ISO-10303-21;");
        _builds = 0;
    }

    [TearDown]
    public void DeleteDir()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private SamplePreparation.Job Job(Action<string, string>? build = null, string producer = "test/1")
        => new("model", _input, _output, build ?? ((_, output) =>
        {
            _builds++;
            File.WriteAllText(output, "db");
        }), producer);

    /// <summary>A job whose output was built by an earlier run.</summary>
    private SamplePreparation.Job Built(string producer = "test/1")
    {
        var job = Job(producer: producer);
        job.Run();
        _builds = 0;
        return job;
    }

    [Test]
    public void MissingOutput_IsStale_AndHasAReason()
    {
        var job = Job();
        Assert.Multiple(() =>
        {
            Assert.That(job.IsCurrent, Is.False);
            Assert.That(SamplePreparation.PendingReason([job])("model"),
                Is.EqualTo("building model.duckdb from model.ifc in the background; it appears when done"));
            Assert.That(SamplePreparation.PendingReason([job])("other"), Is.Null);
        });
    }

    [Test]
    public void BuiltOutput_IsCurrent_AndHasNoReason()
    {
        var job = Built();
        Assert.Multiple(() =>
        {
            Assert.That(job.IsCurrent, Is.True);
            Assert.That(SamplePreparation.PendingReason([job])("model"), Is.Null);
            Assert.That(File.ReadAllText(job.StampPath), Does.Contain("producer: test/1").And.Contain("sha256:"));
        });
    }

    [Test]
    public void CurrentOutput_IsNotRebuilt_EvenWhenOlderThanItsInput()
    {
        var job = Built();
        File.SetLastWriteTimeUtc(_output, File.GetLastWriteTimeUtc(_input).AddMinutes(-1));
        SamplePreparation.Run([job], _ => { }, TextWriter.Null);
        Assert.That(_builds, Is.Zero);
    }

    [Test]
    public void InputChange_Rebuilds()
    {
        var job = Built();
        File.WriteAllText(_input, "ISO-10303-21; changed");
        File.SetLastWriteTimeUtc(_output, File.GetLastWriteTimeUtc(_input).AddMinutes(1));
        Assert.That(job.IsCurrent, Is.False, "a newer output from different input bytes is still stale");
        SamplePreparation.Run([job], _ => { }, TextWriter.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_builds, Is.EqualTo(1));
            Assert.That(job.IsCurrent, Is.True);
        });
    }

    [Test]
    public void ProducerChange_Rebuilds()
    {
        Built(producer: "test/1");
        var job = Job(producer: "test/2");
        Assert.That(job.IsCurrent, Is.False);
        SamplePreparation.Run([job], _ => { }, TextWriter.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_builds, Is.EqualTo(1));
            Assert.That(job.IsCurrent, Is.True);
            Assert.That(Job(producer: "test/1").IsCurrent, Is.False);
        });
    }

    [Test]
    public void StaleStamp_Rebuilds()
    {
        var job = Built();
        File.WriteAllText(job.StampPath, File.ReadAllText(job.StampPath).Replace("sha256:", "sha256:00"));
        Assert.That(job.IsCurrent, Is.False);
        SamplePreparation.Run([job], _ => { }, TextWriter.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_builds, Is.EqualTo(1));
            Assert.That(job.IsCurrent, Is.True);
        });
    }

    [Test]
    public void MissingStamp_RebuildsOnce()
    {
        // An output left by a build from before stamps existed, newer than its input.
        File.WriteAllText(_output, "old");
        File.SetLastWriteTimeUtc(_output, File.GetLastWriteTimeUtc(_input).AddMinutes(1));
        var job = Job();
        Assert.That(job.IsCurrent, Is.False);
        SamplePreparation.Run([job], _ => { }, TextWriter.Null);
        SamplePreparation.Run([job], _ => { }, TextWriter.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_builds, Is.EqualTo(1));
            Assert.That(File.ReadAllText(_output), Is.EqualTo("db"));
            Assert.That(File.Exists(job.StampPath), Is.True);
        });
    }

    [Test]
    public void FailedBuild_LeavesTheOldOutputStale()
    {
        var job = Built(producer: "test/1");
        var next = Job((_, _) => throw new IOException("disk full"), producer: "test/2");
        SamplePreparation.Run([next], _ => { }, TextWriter.Null);
        Assert.Multiple(() =>
        {
            Assert.That(next.IsCurrent, Is.False);
            Assert.That(job.IsCurrent, Is.True, "the old output and its stamp are untouched");
        });
    }

    [Test]
    public void Run_BuildsThroughAPartFile_ThenCallsReady()
    {
        string? builtTo = null;
        var ready = new List<string>();
        var log = new StringWriter();
        var job = Job((_, output) =>
        {
            builtTo = output;
            File.WriteAllText(output, "db");
            Assert.That(File.Exists(_output), Is.False, "the output must not exist while the build is running");
        });
        SamplePreparation.Run([job], j => ready.Add(j.Source), log);
        Assert.Multiple(() =>
        {
            Assert.That(builtTo, Is.EqualTo(_output + SamplePreparation.PartSuffix));
            Assert.That(File.ReadAllText(_output), Is.EqualTo("db"));
            Assert.That(File.Exists(_output + SamplePreparation.PartSuffix), Is.False);
            Assert.That(ready, Is.EqualTo(new[] { "model" }));
            Assert.That(job.IsCurrent, Is.True);
            Assert.That(log.ToString(), Does.Contain("preparing model").And.Contain("ready: model"));
        });
    }

    [Test]
    public void Run_SkipsCurrentJobs()
    {
        var job = Built();
        SamplePreparation.Run([job], _ => { }, TextWriter.Null);
        Assert.That(_builds, Is.Zero);
    }

    [Test]
    public void Run_LogsAFailedJob_AndDoesNotCallReady()
    {
        var ready = false;
        var log = new StringWriter();
        SamplePreparation.Run([Job((_, _) => throw new IOException("disk full"))], _ => ready = true, log);
        Assert.Multiple(() =>
        {
            Assert.That(ready, Is.False);
            Assert.That(log.ToString(), Does.Contain("failed to prepare model: disk full"));
            Assert.That(File.Exists(_output), Is.False);
        });
    }

    [Test]
    public async Task RunInBackground_IsCompleteAtOnceWhenNothingIsStale()
    {
        var task = SamplePreparation.RunInBackground([Built()], _ => { }, TextWriter.Null);
        Assert.That(task.IsCompleted, Is.True);
        await task;
    }
}
