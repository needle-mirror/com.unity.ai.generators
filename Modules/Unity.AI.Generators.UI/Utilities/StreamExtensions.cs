﻿using System;
using System.IO;

namespace Unity.AI.Generators.UI.Utilities
{
    static class StreamExtensions
    {
        public static byte[] ReadFully(this Stream input, bool resetPosition = true)
        {
            if (resetPosition && !input.CanSeek)
                throw new NotSupportedException("The stream is not seekable so its position cannot be reset.");

            long originalPosition = 0;
            if (resetPosition)
            {
                originalPosition = input.Position;
                input.Seek(0, SeekOrigin.Begin);
            }

            byte[] result;
            if (input is MemoryStream ms && ms.TryGetBuffer(out var segment))
            {
                result = segment.Array;
            }
            else
            {
                using var tempStream = new MemoryStream();
                input.CopyTo(tempStream);
                result = tempStream.ToArray();
            }

            if (resetPosition)
                input.Position = originalPosition;

            return result;
        }
    }
}
