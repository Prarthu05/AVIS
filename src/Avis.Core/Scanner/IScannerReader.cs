using System.Threading.Channels;

namespace Avis.Scanner;

/// <summary>Reads decoded barcode text (the operator badge / employee ID) from a scanner.</summary>
public interface IScannerReader
{
    void Start();
    Task StopAsync();
    ChannelReader<string> Reader { get; }
}
