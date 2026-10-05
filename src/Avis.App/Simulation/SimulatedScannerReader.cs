using System.Threading.Channels;
using Avis.Scanner;

namespace Avis.App.Simulation;

/// <summary>Badge scanner stand-in: the SIMULATOR tab's "Scan badge" button pushes an NTID in.</summary>
public class SimulatedScannerReader : IScannerReader
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();

    public ChannelReader<string> Reader => _channel.Reader;

    public void Start()
    {
    }

    public Task StopAsync()
    {
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public void Scan(string employeeId) => _channel.Writer.TryWrite(employeeId);
}
