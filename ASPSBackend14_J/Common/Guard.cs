#nullable enable

using Common;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Common
{
    public class Guard
    {
        public static readonly Guard Against = new();

        private Guard()
        {
        }
    }
}
