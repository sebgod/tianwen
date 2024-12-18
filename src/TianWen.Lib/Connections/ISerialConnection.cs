﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace TianWen.Lib.Connections;

public interface ISerialConnection : IDisposable
{
    internal const string SerialProto = "serial:";

    bool IsOpen { get; }

    Encoding Encoding { get; }

    bool TryClose();

    bool TryWrite(ReadOnlySpan<byte> data);

    bool TryReadTerminated([NotNullWhen(true)] out ReadOnlySpan<byte> message, ReadOnlySpan<byte> terminators);

    bool TryReadExactly(int count, [NotNullWhen(true)] out ReadOnlySpan<byte> message);
}