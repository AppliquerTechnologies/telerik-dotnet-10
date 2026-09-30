using System.Globalization;

namespace TelerikDemo2.Common;

public static class Format
{
    // Fixed en-US style. "C" depends on the server culture and prints the generic currency sign
    // when the host runs with the invariant culture (as the default Docker image does).
    public static string Money(decimal value) => "$" + value.ToString("N2", CultureInfo.InvariantCulture);
}
