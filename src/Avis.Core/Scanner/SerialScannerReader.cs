using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Avis.Configuration;
using Microsoft.Extensions.Logging;

namespace Avis.Scanner;

/// <summary>
/// Reads badge scans from a NetumScan NSA5 / NT-91-NS-91 family scanner in USB-COM
/// / RS232 serial mode. These scan engines stream each decoded barcode as ASCII
/// text terminated by a suffix byte (CR by default, per the vendor's Serial SDK) -
/// we just need to read and frame those lines.
/// </summary>
public class SerialScannerReader : IScannerReader
{
    private readonly ScannerOptions _options;
    private readonly ILogger<SerialScannerReader> _logger;
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();
    private readonly Regex? _employeeIdRegex;
    private readonly byte[] _terminator;

    private CancellationTokenSource? _cts;
    private Task? _runTask;

    public ChannelReader<string> Reader => _channel.Reader;

    public SerialScannerReader(ScannerOptions options, ILogger<SerialScannerReader> logger)
    {
        _options = options;
        _logger = logger;
        _employeeIdRegex = string.IsNullOrEmpty(options.EmployeeIdRegex) ? null : new Regex(options.EmployeeIdRegex);
        _terminator = options.LineTerminator.ToUpperInvariant() switch
        {
            "CRLF" => new byte[] { 0x0D, 0x0A },
            "LF" => new byte[] { 0x0A },
            _ => new byte[] { 0x0D },
        };
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _runTask = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_runTask is not null)
        {
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var buffer = new List<byte>();

        while (!ct.IsCancellationRequested)
        {
            SerialPort? port = null;
            try
            {
                port = new SerialPort(_options.Port, _options.BaudRate, ParseParity(_options.Parity), _options.DataBits, ParseStopBits(_options.StopBits))
                {
                    ReadTimeout = Math.Max(100, (int)(_options.ReadTimeoutSeconds * 1000)),
                };
                port.Open();
                _logger.LogInformation("Scanner connected on {Port} @ {BaudRate} baud", _options.Port, _options.BaudRate);
                buffer.Clear();

                var readBuffer = new byte[256];
                while (!ct.IsCancellationRequested)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = port.Read(readBuffer, 0, readBuffer.Length);
                    }
                    catch (TimeoutException)
                    {
                        continue;
                    }

                    if (bytesRead <= 0)
                    {
                        continue;
                    }

                    for (var i = 0; i < bytesRead; i++)
                    {
                        buffer.Add(readBuffer[i]);
                    }
                    ExtractLines(buffer);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                _logger.LogWarning(
                    "Scanner serial error on {Port}: {Error} (retrying in {Delay}s)",
                    _options.Port, ex.Message, _options.ReconnectDelaySeconds);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            finally
            {
                try
                {
                    if (port is { IsOpen: true })
                    {
                        port.Close();
                    }
                    port?.Dispose();
                }
                catch
                {
                    // best-effort cleanup
                }
            }
        }
    }

    internal void ExtractLines(List<byte> buffer)
    {
        int index;
        while ((index = IndexOfTerminator(buffer)) >= 0)
        {
            var lineBytes = buffer.GetRange(0, index).ToArray();
            buffer.RemoveRange(0, index + _terminator.Length);
            HandleLine(lineBytes);
        }
    }

    private int IndexOfTerminator(List<byte> buffer)
    {
        if (buffer.Count < _terminator.Length)
        {
            return -1;
        }
        for (var i = 0; i <= buffer.Count - _terminator.Length; i++)
        {
            var isMatch = true;
            for (var j = 0; j < _terminator.Length; j++)
            {
                if (buffer[i + j] != _terminator[j])
                {
                    isMatch = false;
                    break;
                }
            }
            if (isMatch)
            {
                return i;
            }
        }
        return -1;
    }

    private void HandleLine(byte[] rawLine)
    {
        var text = Encoding.UTF8.GetString(rawLine).Trim();
        if (text.Length == 0)
        {
            return;
        }

        var value = text;
        if (_employeeIdRegex is not null)
        {
            var match = _employeeIdRegex.Match(text);
            if (!match.Success)
            {
                _logger.LogWarning("Scanned barcode {Text} did not match EmployeeIdRegex; ignoring", text);
                return;
            }
            value = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
        }

        _logger.LogInformation("Scanner read: {Value}", value);
        _channel.Writer.TryWrite(value);
    }

    private static Parity ParseParity(string value) => Enum.Parse<Parity>(value, ignoreCase: true);

    private static StopBits ParseStopBits(string value) => Enum.Parse<StopBits>(value, ignoreCase: true);
}
