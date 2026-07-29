namespace Microsoft.Mutate4CSharp.Report;

using Microsoft.Mutate4CSharp.Model;

/// <summary>
/// A progress reporter that writes each formatted line to a <see cref="TextWriter"/>. Faithful port
/// of mutate4java's <c>PrintStreamProgressReporter</c>: writes are serialized so concurrent workers
/// never interleave a line. mutate4java's <c>synchronized</c> methods map to a private lock.
/// </summary>
public sealed class PrintStreamProgressReporter : IProgressReporter
{
    private readonly TextWriter _out;
    private readonly ProgressMessageFormatter _formatter = new();
    private readonly object _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PrintStreamProgressReporter"/> class.
    /// </summary>
    /// <param name="output">The writer that receives the progress lines.</param>
    public PrintStreamProgressReporter(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _out = output;
    }

    /// <inheritdoc/>
    public void BaselineStarting(string moduleRoot)
    {
        lock (_gate)
        {
            _out.Write(_formatter.BaselineStarting(moduleRoot));
        }
    }

    /// <inheritdoc/>
    public void BaselineFinished(TestRun baseline)
    {
        lock (_gate)
        {
            _out.Write(_formatter.BaselineFinished(baseline));
        }
    }

    /// <inheritdoc/>
    public void RunStarting(int totalMutations, int workerCount)
    {
        lock (_gate)
        {
            _out.Write(_formatter.RunStarting(totalMutations, workerCount));
        }
    }

    /// <inheritdoc/>
    public void MutationStarting(int workerIndex, MutationJob job)
    {
        lock (_gate)
        {
            _out.Write(_formatter.MutationStarting(workerIndex, job));
        }
    }

    /// <inheritdoc/>
    public void MutationFinished(int workerIndex, MutationResult result)
    {
        lock (_gate)
        {
            _out.Write(_formatter.MutationFinished(workerIndex, result));
        }
    }
}
