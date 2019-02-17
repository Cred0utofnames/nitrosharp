﻿using System;
using System.IO;

namespace NitroSharp.NsScript
{
    public sealed class DependencyResolutionException : Exception
    {
        public DependencyResolutionException()
        {
        }

        public DependencyResolutionException(string message) : base(message)
        {
        }

        public DependencyResolutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public static class ThrowHelper
    {
        public static T IllegalValue<T>(string message)
            => throw new ArgumentException(message);

        public static void Unreachable()
            => throw new InvalidOperationException("This program location is expected to be unreachable.");

        public static T Unreachable<T>()
            => throw new InvalidOperationException("This program location is expected to be unreachable.");

        public static void ThrowOutOfRange(string paramName)
            => new ArgumentOutOfRangeException(paramName);

        public static Exception UnexpectedValue(string paramName)
            => new ArgumentException("Unexpected value.", paramName);

        public static T InvalidData<T>(string message)
            => throw new InvalidDataException(message);
    }
}
