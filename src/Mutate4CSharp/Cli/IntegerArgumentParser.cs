namespace Microsoft.Mutate4CSharp.Cli;

using System.Globalization;

/// <summary>
/// Parses a positive integer flag value, rejecting non-numeric and non-positive input with a
/// uniform message. Parsing is culture-invariant.
/// </summary>
public sealed class IntegerArgumentParser
{
    /// <summary>
    /// Parses <paramref name="text"/> as a positive integer.
    /// </summary>
    /// <param name="text">The raw value.</param>
    /// <param name="flag">The originating flag name, used in the error message.</param>
    /// <returns>The parsed positive integer.</returns>
    public int ParsePositiveInt(string text, string flag)
    {
        if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
        {
            if (value <= 0)
            {
                throw new ArgumentException(flag + " must be a positive integer");
            }

            return value;
        }

        throw new ArgumentException(flag + " must be a positive integer");
    }
}
