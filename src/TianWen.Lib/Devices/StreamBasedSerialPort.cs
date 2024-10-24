﻿using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Ports;
using System.Text;

namespace TianWen.Lib.Devices;

public sealed class StreamBasedSerialPort : IDisposable
{
    private readonly SerialPort _port;
    private readonly Stream _stream;
    private readonly ILogger _logger;

    public StreamBasedSerialPort(string portName, int baud, ILogger logger, Encoding encoding, TimeSpan? ioTimeout = null)
    {
        _port = new SerialPort(portName, baud);
        _port.Open();

        var timeoutMs = (int)(ioTimeout ?? TimeSpan.FromMicroseconds(500)).TotalMilliseconds;
        _stream = _port.BaseStream;
        _stream.ReadTimeout  = timeoutMs;
        _stream.WriteTimeout = timeoutMs;

        _logger = logger;
        Encoding = encoding;
    }

    public bool IsOpen => _port.IsOpen;

    /// <summary>
    /// Encoding used for decoding byte messages (used for display/logging only)
    /// </summary>
    public Encoding Encoding { get; }

    /// <summary>
    /// Closes the serial port if it is open
    /// </summary>
    /// <returns>true if the prot is closed</returns>
    public bool TryClose()
    {
        if (_port.IsOpen)
        {
            _port.Close();
        }
        return !_port.IsOpen;
    }

    public bool TryWrite(ReadOnlySpan<byte> message)
    {
        try
        {
            _stream.Write(message);
#if DEBUG
            _logger.LogDebug("--> {Message}", Encoding.GetString(message));
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error {ErrorMessage} while sending message {Message} to serial device on port {Port}",
                ex.Message, Encoding.GetString(message), _port.PortName);

            return false;
        }

        return true;
    }

    public bool TryReadTerminated([NotNullWhen(true)] out ReadOnlySpan<byte> message, char terminator)
    {
        Span<byte> buffer = stackalloc byte[100];
        try
        {
            int bytesRead = 0;
            int bytesReadLast;
            do
            {
                bytesReadLast = _stream.ReadAtLeast(buffer[bytesRead..], 1, true);
                bytesRead += bytesReadLast;
            } while (buffer[bytesRead - bytesReadLast] != terminator);

            message = buffer[0..(bytesRead - bytesReadLast -1)].ToArray();
#if DEBUG
            _logger.LogDebug("<-- (terminated {Terminator}): {Response}", terminator, Encoding.GetString(message));
#endif
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error {ErrorMessage} while reading response from serial device on port {Port}", ex.Message, _port.PortName);

            message = null;
            return false;
        }
    }

    public bool TryReadExactly(int count, [NotNullWhen(true)] out ReadOnlySpan<byte> message)
    {
        Span<byte> buffer = count > 100 ? new byte[count] : stackalloc byte[count];
        try
        {
            _stream.ReadExactly(buffer);

            message = buffer.ToArray();
#if DEBUG
            _logger.LogDebug("<-- (exactly {Count}): {Response}", count, Encoding.GetString(message));
#endif
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error {ErrorMessage} while reading response from serial device on port {Port}", ex.Message, _port.PortName);

            message = null;
            return false;
        }
    }

    public void Dispose() => _ = TryClose();
}