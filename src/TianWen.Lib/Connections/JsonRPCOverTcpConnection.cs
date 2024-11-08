﻿/*

MIT License

Copyright (c) 2018 Andy Galasso

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace TianWen.Lib.Connections;

internal class JsonRPCOverTcpConnection() : IUtf8TextBasedConnection
{
    private TcpClient? _tcpClient;
    private StreamReader? _streamReader;

    public void Connect(EndPoint endPoint)
    {
        if (endPoint is not IPEndPoint ipEndPoint)
        {
            throw new ArgumentException($"{endPoint} address familiy {endPoint.AddressFamily} is not supported", nameof(endPoint));
        }

        _tcpClient = new TcpClient();
        _tcpClient.Connect(ipEndPoint);
        _streamReader = new StreamReader(_tcpClient.GetStream());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _streamReader?.Close();
            _streamReader = null;

            _tcpClient?.Close();
            _tcpClient = null;
        }
    }

    public bool IsConnected => _tcpClient?.Connected is true;

    public CommunicationProtocol HighLevelProtocol => CommunicationProtocol.JsonRPC;

    public string? ReadLine() => _streamReader?.ReadLine();

    public bool WriteLine(ReadOnlyMemory<byte> jsonlUtf8Bytes)
    {
        Span<byte> CRLF = [(byte)'\r', (byte)'\n'];

        if (_tcpClient?.GetStream() is NetworkStream stream && stream.CanWrite)
        {
            stream.Write(jsonlUtf8Bytes.Span);
            stream.Write(CRLF);
            stream.Flush();
            return true;
        }

        return false;
    }
}
