using BimOpenToolkit.TestSupport;

namespace BimOpenFlow.Host.Tests;

/// <summary>Background preparation of generated samples: staleness, the "not ready yet"
/// reason, building through a .part file, and the ready callback. The build is injected so
/// these run in milliseconds; the real IFC build is covered by IfcDuckDbBuildTests and the
/// NRC workflow fixture.</summary>
[TestFixture]
public sealed class SamplePreparationTests
{
    private string _dir = null!;
    private string _input = null!;
    private string _output = null!;

    [SetUp]
    public void NewDir()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bof-preparation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _input = Path.Combine(_dir, "model.ifc");
        _output = Path.Combine(_dir, "model.duckdb");
        File.WriteAllText(_input, "ISO-10303-21;");
    }

    [TearDown]
    public void DeleteDir()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    private SamplePreparation.Job Job(Action<string, string>? build = null)
        => new("model", _input, _output, build ?? ((_, output) => File.WriteAllText(output, "db")));

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
    public void OutputNewerThanInput_IsCurrent_AndHasNoReason()
    {
        File.WriteAllText(_output, "db");
        File.SetLastWriteTimeUtc(_output, File.GetLastWriteTimeUtc(_input).AddMinutes(1));
        var job = Job();
        Assert.Multiple(() =>
        {
            Assert.That(job.IsCurrent, Is.True);
            Assert.That(SamplePreparation.PendingReason([job])("model"), Is.Null);
        });
    }

    [Test]
    public void OutputOlderThanInput_IsStale()
    {
        File.WriteAllText(_output, "old");
        File.SetLastWriteTimeUtc(_output, File.GetLastWriteTimeUtc(_input).AddMinutes(-1));
        Assert.That(Job().IsCurrent, Is.False);
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
        File.WriteAllText(_output, "db");
        File.SetLastWriteTimeUtc(_output, File.GetLastWriteTimeUtc(_input).AddMinutes(1));
        var built = false;
        SamplePreparation.Run([Job((_, _) => built = true)], _ => { }, TextWriter.Null);
        Assert.That(built, Is.False);
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
        File.WriteAllText(_output, "db");
        File.SetLastWriteTimeUtc(_output, File.GetLastWriteTimeUtc(_input).AddMinutes(1));
        var task = SamplePreparation.RunInBackground([Job()], _ => { }, TextWriter.Null);
        Assert.That(task.IsCompleted, Is.True);
        await task;
    }

    [Test]
    public void NrcDatabaseJob_NamesTheSourceTheGraphsUse()
    {
        var root = RepoPaths.Root;
        var job = SamplePreparation.NrcDatabase(root);
        Assert.That(job, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(job!.Source, Is.EqualTo("duplex-enriched"));
            Assert.That(Path.GetFileName(job.Input), Is.EqualTo(SampleSeeding.NrcIfcFileName));
            Assert.That(Path.GetFileName(job.Output), Is.EqualTo(SampleSeeding.NrcDatabaseFileName));
            Assert.That(Path.GetDirectoryName(job.Output), Is.EqualTo(SampleSeeding.NrcSamplesDir(root)));
        });
    }
}
