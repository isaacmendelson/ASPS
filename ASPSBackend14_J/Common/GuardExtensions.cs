#nullable enable

using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public static class GuardExtensions
    {

        public static T Null<T>(this Guard _, T? value, 
            [CallerArgumentExpression("value")] string? name = default)
        {
            if (value is null)
            {
                throw new ArgumentNullException(name);
            }

            return value;
        }
         

        public static T Default<T>(this Guard _, T value,
            [CallerArgumentExpression("value")] string? name = default) where T : struct
        {
            if (value.Equals(default(T)))
            {
                throw new ArgumentException($"{name ?? "argument" } cannot have a default value.");
            }

            return value;
        }

        public static T NullOrDefault<T>(this Guard _, T? value,
            [CallerArgumentExpression("value")] string? name = default) where T : struct
        {
            if (value is null)
            {
                throw new ArgumentNullException(name);
            }

            if (value.Equals(default(T)))
            {
                throw new ArgumentException($"{name ?? "argument"} cannot have a default value.");
            }

            return value.Value;
        }

        public static T NullOrNotOfType<T>(this Guard _, object? value,
            [CallerArgumentExpression("value")] string? name = default) where T : class
        {
            if (value is null)
            {
                throw new ArgumentNullException(name);
            }

            if (value is not T t)
            {
                throw new ArgumentException($"{name ?? "argument"} is not of type '{typeof(T).Name}'.", name);
            }

            return t;
        }

        public static string NullOrWhitespace(this Guard _, string? value,
            [CallerArgumentExpression("value")] string? name = default)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"'{name ?? "argument"}' cannot be null or whitespace.", nameof(value));
            }
            return value;
        }

        public static string? NotNullButEmpty(this Guard _, string? value,
            [CallerArgumentExpression("value")] string? name = default)
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"'{name ?? "argument"}' cannot be null or whitespace.", nameof(value));
            }
            return value;
        }

        public static IEnumerable<T> NullOrEmpty<T>(this Guard _, IEnumerable<T>? value,
            [CallerArgumentExpression("value")] string? name = default)
        {
            if (value is null)
            {
                throw new ArgumentNullException(name);
            }

            if (!value.Any())
            {
                throw new ArgumentException($"{name?? "argument"} cannot be empty", name);
            }

            return value;
        }

        public static T UndefinedEnumeration<T>(this Guard _, T value,
            [CallerArgumentExpression("value")] string? name = default) where T : Enumeration
        {
            if (Enumeration.FindByValue<T>(value.Value) is null)
            {
                throw new ArgumentException($"Value '{value}' is not defined in '{typeof(T).Name}'.",
                    name);
            }
            return value;
        }

        public static T UndefinedEnum<T>(this Guard _, T value,
            [CallerArgumentExpression("value")] string? name = default) where T : struct, Enum
        {
            if (typeof(T).GetCustomAttributes(typeof(FlagsAttribute), false).Any())
            {
                // TODO: check flags
                return value;
            }

            if (!Enum.IsDefined(value))
            {
                throw new ArgumentException($"Value '{value}' is not defined in '{typeof(T).Name}'.",
                    name);
            }
            return value;
        }

        public static int OutOfRange(this Guard _, int value, int min, int max, 
            [CallerArgumentExpression("value")] string? name = default)
        {
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(name, value, 
                    $"{name ?? "argument"} must be greater than {min} " +
                    $"and lower than {max}.");
            }
            return value;
        }

        public static double OutOfRange(this Guard _, double value, double min, double max,
            [CallerArgumentExpression("value")] string? name = default)
        {
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(name, value,
                    $"{name ?? "argument"} must be greater than {min} " +
                    $"and lower than {max}.");
            }
            return value;
        }

        public static void False(this Guard _, bool condition, string name, string message)
        {
            if (!condition)
            {
                throw new ArgumentException(message, name);
            }
        }

        public static string StringLengthOutOfRange(this Guard guard, string value, int min, int max,
            [CallerArgumentExpression("value")] string? name = default)
        {
            _ = Guard.Against.Null(value);
            if (value.Length < min || value.Length > max)
            {
                throw new ArgumentException("String length is out of range", name);
            }
            return value;
        }
    }
}
